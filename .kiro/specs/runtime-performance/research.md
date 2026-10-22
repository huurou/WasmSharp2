# 調査と設計判断

## 概要（Summary）

- **対象:** runtime-performance
- **調査日:** 2026-10-22
- **調査範囲:** 既存ランタイムへの拡張。性能に関わる内部処理と新規測定依存を扱うため、light discoveryの条件に従ってfull discoveryへ拡張した。
- **調査時のHEAD:** `f4c91f7a47e727aac1fd05e5f040e34cb5076fdd`。既存のroadmap変更、runtime-performanceのbrief/requirements、性能調査ノートを入力として参照した。
- **主要な発見:**
  - 現在のフレームと値スタックは`InterpreterContext`へ集約されている。非空結果の独立配列と`WasmResults`でコピーが重なる経路はあるが、性能寄与は未測定である。
  - BenchmarkDotNet 0.15.8は.NET10を扱える。時間と割り当ては異なる反復で観測され、複数launchのGC情報は最後の実行分になるため、1launchの独立runを反復する。
  - Full JSONだけでは成否、子process終了値、全Job条件、割り当てraw総量が不足する。小さなrun記録と前後の期待値確認で補完する。

## 調査記録（Research Log）

### 入力・適用規則

- **背景:** 既存の公開契約と測定・受入の要件を設計へ引き継ぐ。
- **参照:** [要件](requirements.md)、[ブリーフ](brief.md)、[性能調査ノート](../../../docs/research/wasm-performance-measurement.md)、`CONTEXT.md`、core steering、ADR0001〜0012の関連判断。
- **適用したスキル:** `kiro-spec-design`のfull discovery・synthesis・review gate、`csharp-conventions`の型・配置・テスト規則、`codebase-design`の小さいInterfaceと不要なAdapterを増やさない判断、`kiro-verify-completion`の設計生成に限定した完了確認。
- **結果:** 要件は8節44受入基準、言語はja。`-y`により要件承認を更新した。設計承認、タスク生成、実装開始は今回の処理へ含めない。
- **含意:** 合意済みの要件は変更せず、数値目標を測定後かつ最適化前に確定する工程を設計へ置く。

### 現在の公開4段階と所有

- **参照:** `src/WasmSharp/WasmModule.cs`、`WasmFunction.cs`、`WasmResults.cs`、`Execution/Interpreter.cs`、`ExecutionBoundary.cs`、`InterpreterContext.cs`、`Modules/ModuleInstantiator.cs`。
- **確認事項:**
  - Decodeは静的定義を作り、Validateの成功時だけコードと検証状態が確定する。検証済みmoduleの再呼び出しはValidate測定にならない。
  - Instantiateにはリンク・資源生成・element/data・startが含まれる。測定側で内部段階だけを切り出さない。
  - 現在の実行状態は`WasmValue[] values_`と`ExecutionFrame[] frames_`である。旧構成を前提としたValueStack/ControlStack/CallFrameの追加を計画しない。
  - `Interpreter.Run`の`CopyValues`と`ExecutionBoundary`の`new WasmResults`は結果コピーが重なる経路である。公開constructorのコピー契約を維持した内部受渡しは候補となる。
  - host引数コピーは、元引数の変更や同期再入からcallbackを保護する。直接callは既に共有値スタックを使っている。
- **含意:** ソース上の割り当て候補をそのまま改善順位へ置き換えない。結果所有・作業領域・呼び出し・memory/SIMD・準備処理を測ってから1〜3箇所を選ぶ。

### 命令・memory・SIMD

- **参照:** `Execution/InterpreterContext.cs`、`Execution/Instruction.cs`、`src/WasmSharp.Generators/InstructionGenerator.cs`、`WasmMemory.cs`、`Execution/Interpreter.Memory.cs`、`Interpreter.SimdMemory.cs`、`Interpreter.SimdInteger.cs`、`Interpreter.SimdFloat.cs`、`SimdValue.cs`。
- **確認事項:**
  - 生成RunLoopは命令を取得して単一switchからhandlerを呼ぶ。Source GeneratorはJIT/AOTではなく、性能優位を示す測定も今回行っていない。
  - Popや分岐・return時の値クリアは参照寿命に関わる。削除は無条件の高速化ではない。同期再入で配列が拡張されるため、配列内のrefをcallback越しに保持する変更には寿命検証が必要である。
  - memoryは64KiBページの配列で、全幅の範囲確認後に転送する。scalar/SIMDは同じリソースへ接続する。
  - SIMDにはlaneごとのGetLane/WithLaneと関数呼び出しがある。static lambdaが使われているため、毎命令のdelegate割り当てとは断定できない。
- **含意:** 命令取得や生成器の変更まで必要になった場合は、測定根拠と対象ファイルを改善計画・設計へ追加してから進める。初期設計は生成器の変更を前提としない。

### BenchmarkDotNetの固定版とJob

- **参照:** [CoreRuntime](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Environments/Runtimes/CoreRuntime.cs)、[JobExtensions](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Jobs/JobExtensions.cs)、[PowerManagementApplier](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Running/PowerManagementApplier.cs)。
- **確認事項:** `CoreRuntime.Core10_0`を持つ。`DontEnforcePowerPlan()`は電源plan強制を無効にする。通常の既定はHighPerformance適用であり、単に現在のplanを記録するだけでは不足する。
- **採用:** 0.15.8固定、Throughput、x64、1launch、MemoryDiagnoser、MemoryRandomization無効、`DontEnforcePowerPlan()`。SDK/runtime・実効Job・環境をrunへ記録する。
- **未実施:** package導入、別processの最小ケース実行。このリポジトリでの動作確認は実装最初の前提確認とする。現在のSDKは10.0.402。

### ライフサイクルと期待値確認

- **参照:** [EngineFactory](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/EngineFactory.cs)、[Engine](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs)、[EngineResolver](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/EngineResolver.cs)、[Setup/Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)、[Consumer](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Consumer.cs)。
- **確認事項:** GlobalSetup後にJitting・pilot・overhead・warmup・本測定を行い、割り当て観測の追加反復後にGlobalCleanupを行う。cleanupの最後の戻り値は通常この追加反復の結果である。IterationSetupは短い処理へ機械的に追加しない。
- **採用:** 固定入力の読み取りまたは出力上書きなど、全反復段階で処理量を保つ素材を使う。計時外で前後検査を行い、計時内はConsumerで返却objectを消費する。最後の結果1件のfield保持を測定定義に含める。
- **含意:** 準備時だけのresetでは不十分。growやValidate単体を追加するときは、未使用の入力バッチと1回の測定呼出しを明示した別Jobにする。

### 指標・統計・成否

- **参照:** [GcStats](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs)、[MemoryDiagnoser](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Diagnosers/MemoryDiagnoser.cs)、[BenchmarkReport](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Reports/BenchmarkReport.cs)、[Statistics](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Mathematics/Statistics.cs)、[RunResults](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/RunResults.cs)、[ExecuteResult](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Toolchains/Results/ExecuteResult.cs)、[JsonExporterBase](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Exporters/Json/JsonExporterBase.cs)。
- **確認事項:**
  - .NET10の割り当て経路は`GC.GetTotalAllocatedBytes(precise: true)`の差分。古い一般資料のcurrent-thread説明より固定版の実装を優先する。
  - 公開bytes/opはToEvenで整数に丸める。raw総量と操作数を併記する。
  - 時間統計はWorkload/Resultのバッチ平均を使い、99.9%信頼区間を持つ。Throughput既定はRemoveUpperで、overhead控除後のResultとActualを区別する。
  - 時間は全launchの測定を集める一方、reportのGcStatsは最後のExecuteResultに由来する。
  - ExecuteResult.IsSuccessは結果行の存在を見ており、終了値0を保証しない。GlobalCleanup例外もエラー送出へ変換される。
  - Full JSONにSuccess、終了値、Errors、全Job設定、割り当てraw総量は揃わない。
- **含意:** 1launchの独立runを反復する。結果採用はreport・子process終了値・Errors・必須指標・process別前後検査を突合する。独自記録は不足分だけに絞り、時間の計時と統計はBDNを利用する。

### 初期素材の選定

- **参照:** [既存調査](../../../docs/research/wasm-performance-measurement.md)、[Clang freestanding](https://clang.llvm.org/docs/UsersManual.html#freestanding-builds)、[LLD WebAssembly](https://lld.llvm.org/WebAssembly.html)、[LLVM 21.1.8発表](https://discourse.llvm.org/t/llvm-21-1-8-released/89144)、[外部ツールの固定](../../../thirdParties/README.md)。
- **比較:** wasmi系素材は候補だが、初期の要件は第三者suiteの採用を要求しない。自作WATと自作Cで入力・期待値・状態・importを小さく固定できる。
- **採用:** 5個のWAT由来moduleと1個のC由来module、計15測定ケース。直接callと整数分岐、host callbackと浮動小数点処理をそれぞれ同じケースで代表させる。scalar/SIMDは同じ入力・仕事量とする。
- **制約:** freestandingだけではlibc呼び出しの不存在を保証しない。未解決symbolを許容せず、importとfeatureを生成物で検査する。Clang/LLD 21.1.8の指定実行ファイルを必要とするが、設計時点のPATHでは確認できていない。生成環境の準備は実装手順へ含める。
- **権利・表示:** 自作素材の出典と条件を記録し、外部素材へ差し替える場合は利用・再配布条件を個別確認する。上流toolのライセンスと素材そのものの条件を混同しない。

### 既存の回帰確認との接続

- **参照:** [公式ランナーREADME](../../../tools/WasmSharp.TestSuiteRunner/README.md)、[SIMD受入記録10.3](../simd/acceptance.md#103-比較受入済みrunの明示baseline保存)、[固定profile](../../../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)。
- **確認事項:** 全体run・compare-run・verifyが独立した既存操作として利用できる。最新の受入記録が指すbaselineは`artifacts/simd/task-10.3-20261018T215944Z/run-baseline.json`。対応manifestとbaselineの存在を確認したが、今回再実行や再hashはしていない。
- **含意:** 性能baselineと公式baselineを分離する。今回の設計生成ではビルド・テスト・公式runを実行せず、実装完了時の必須手順として設計に残す。

## アーキテクチャ案の評価（Architecture Pattern Evaluation）

| 案 | 長所 | 制約 | 判断 |
| --- | --- | --- | --- |
| 独立BDNツールと小さい記録・比較処理 | 公開APIの利用、統計・process分離の再利用、ランタイム依存の分離 | 成否と再現情報は補完が必要 | 採用 |
| Stopwatch主体の専用計測器 | 初期コードを小さくできる | warmup・overhead・統計の責務を新たに持つ | 不採用 |
| 公式ランナーに性能機能を追加 | 素材・結果保存の一部を共有できる | 公式判定と性能case・反復条件が結合する | 不採用 |
| 複数エンジンadapterと外部suite導入 | 他エンジン比較を拡張しやすい | 初期要件外の抽象化と素材移植が増える | 後続へ分離 |

## 設計判断（Design Decisions）

### 判断: ケース定義を比較単位へ一般化する

- **背景:** 素材、入力、期待値、区間、操作数、状態がそれぞれ比較可否に影響する。
- **選択:** `BenchmarkCase`と素材manifestに一度だけ記録し、測定・前後検査・保存・比較が参照する。
- **却下:** ベンチメソッド、README、比較scriptごとに期待値と操作数を重複させる構成。
- **効果:** schema/ケースrevisionで変更を識別し、同じcase IDだけによる誤比較を避ける。汎用DSLやplugin登録機構は作らない。

### 判断: 計測は採用し、成否と再現情報だけ補う

- **背景:** BDNは必要な計時・統計を持つが、全要件の保存契約までは扱わない。
- **選択:** BDN reportとexportを利用し、run manifest、process別検査、raw割り当て総量、条件照合を独自処理にする。
- **代替:** BDN JSONだけで成功判定する案は終了異常を見落とし得る。全体を自作する案は不要な責務が増える。
- **後続確認:** 最初の小さい別process smokeで正常、後検査失敗、欠落指標を検証する。

### 判断: 最適化方式より先に測定と数値基準を確定する

- **背景:** 結果コピー等の候補は見えているが、寄与は未測定である。
- **選択:** 同版3run以上→profile→1〜3箇所の選定と事前policy→責務整理→最適化→同条件比較とする。
- **代替:** 再利用やSIMD特殊化を設計時点で義務化する案は採らない。
- **簡素化:** 数値目標は一律値を発明せず、測定後に既定様式で確定する。抽象的な性能frameworkやpoolは前提にしない。
- **後続確認:** 少なくとも1件の時間または割り当ての改善、全ケースへの影響、必要な保持量観測、公式回帰確認が揃って初めて完了する。

## リスクと対策（Risks & Mitigations）

- **短いInvokeをharnessが支配する:** 計時内の結果保持・消費を固定し、case switch・hash・ログを置かない。結果が測定分解能で識別できなければ判定不能とする。
- **可変状態で仕事量が変わる:** pilotや割り当て追加反復も含めて固定入力・上書き・固定点を維持する。
- **再利用で不要な参照が残る:** 論理状態とbuffer寿命を分け、全終了経路の復元と保持量・rootの観測を行う。
- **数値結果や診断が変わる:** 既存直接テストと公式の全体run・比較・verifyを利用し、期待値等を緩めない。
- **生成ツールが未準備:** 版・実行ファイル指定、Core 2.0検査、公開APIでの期待値確認を初期工程へ入れ、測定と混ぜない。
- **既存結果を上書きする:** 新しい専用ディレクトリだけを許し、採用基準値はrun ID/hashで参照する。

## 設計レビュー（Design Review Gate）

2026-10-22に、最終文書の保存前の設計案へ機械確認と独立レビューを行い、PASSとなった。

- 機械確認: 44受入基準に対して44件の対応。欠落、余分なID、重複、必要節の欠落、placeholderは0件。ローカル参照先の欠落も0件。
- 独立レビュー: 履歴を引き継がないreviewerが要件・設計案・steeringとBDN固定版の一次ソースを照合した。責務境界、file配置、測定状態、成否、比較、実装順序に重大な指摘はなく、要件へ戻すgapもなかった。
- 親側の確認: 44件の対応、具体的な変更候補の存在、境界と依存方向、所有・寿命の維持、測定前に最適化を確定しない順序を確認した。
- 設計生成時のレビュー対象のSHA-256: `DD5C5FF965B7E815636CB4BAD98CBCEB7171DEA7C6EFEB115A34569CA0FBB58C`。この内容を`design.md`へ保存した。以下の追加レビューによる修正前のhashであり、現在の文書のhashとは区別する。
- 確認範囲: 設計生成の完了のみ。package導入、素材生成、ビルド、テスト、性能測定、公式スイート実行は未実施であり、実装完了や性能改善の成立を主張しない。

### Claude Codeの追加レビューへの対応

2026-10-22に未コミットの7文書を読み取り専用でレビューした。初回セッションは`4352cd67-42fe-4b4a-82b1-9792d0351414`で、終了値0と最終回答を確認した。初回所見の採否は以下のとおり。承認済み要件や設計の承認状態は変更しない。

1. **時期の離れた基準値との比較（High）: 一部採用。** 同じ測定期間で基準revisionと候補revisionを交互に各3回以上測り、保存sourceから比較元を復元する手順を追加した。環境差を判定不能とする契約は既にあるため、環境差を許容してしまうという評価とHighの重大度は採らず、Mediumの手順補足と判断した。
2. **複数改善の比較元と累積悪化（Medium）: 採用。** 変更ごとは直前に受け入れた状態、最終評価は元の基準状態を使うことと、最初の許容幅で累積悪化を判断することを明記した。既存の比較要求は別policyを指定できるため、policyを1つしか使えないという説明は採らない。
3. **判定不能と完了判定の関係（Medium）: 一部採用。** 条件差・正しさ不成立・測定不足・ばらつき超過は完了を阻害し、有効な比較で許容内だが目標未達等の場合は改善件数に数えないことを追記した。判断理由の保存は既存契約を使い、新しい理由コード型は追加しない。
4. **公式検証後の修正で性能再評価がない（Medium）: 欠陥としては不採用。** 既存の完了条件は性能・公式検証の双方を最終版で行うことを要求している。誤読を避ける補足として、公式検証への対応後に全15ケースを再比較する一文とフローの戻りを加えた。
5. **SIMDのCPU支援方針と再検証（Medium）: 採用。** 先行SIMD設計の方針と、SIMD処理を変更した場合の`DOTNET_EnableHWIntrinsic=0`による関連テストを再検証条件へ引き継いだ。
6. **新テストprojectの標準手順・CI反映漏れ（Low）: 一部採用。** 現行CIは3projectを個別列挙しているため、README・workflow・techの更新を計画に追加した。既存の配置原則は変えないためstructureの変更は必須としない。
7. **候補ファイルと再検証対象の漏れ（Low）: 一部採用。** 生成器テストが埋め込むランタイムsourceの変更も生成器テストの再実行条件へ加えた。候補一覧は非網羅であり、変更前に対象・関連テストを限定する既存手順があるため、全候補と全テストの列挙は追加しない。
8. **公式baseline保存の扱い（Low）: 採用。** 性能と公式検証の受入後、比較済みの同じrunを新しい出力先へ明示保存し、従来のbaselineを残す手順を加えた。
9. **WABTの必要ツールの準備（Low）: 一部採用。** 既存手順はwast2jsonだけなので、wat2wasm・wasm-validateのReleaseビルドと指定pathを準備手順へ加えた。実行ファイルhashの保存自体は既存契約にある。
10. **文書間の不整合（Low）: 一部採用。** コンポーネント表の要件担当を対応表に揃え、計画上のテスト名を契約のRunに合わせ、ロードマップの8仕様がCore 2.0への対応を指すことを明記した。要件の「測定失敗」はenum名の指定ではなく、中断をIncompleteとして改善から除外する既存設計は整合するため維持した。
11. **子processへの識別情報伝達と比較条件（Low・推測）: 設計補足を採用。** run設定のpathを専用環境変数で渡し、識別情報を比較対象から分け、子processが読み込んだassemblyのhashを記録する方針を追加した。未実装の比較処理が必ず失敗するとは判断しない。

任意の提案であるケース分割、Instantiate overloadの明示、診断手段の追加固定、startなしケースの不採用理由、run数の追加規則は、初期15ケースの選定と既存の原因調査・ばらつき判断を変える必要がないため今回の修正へ含めない。

Codex側では、BDN固定版の[Engine](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs)、[GcStats](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs)、[ExecuteResult](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Toolchains/Results/ExecuteResult.cs)、[JobExtensions](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Jobs/JobExtensions.cs)も照合した。追加反復の割り当て観測、終了値とErrors、Job環境変数の扱いを確認し、文書修正に限定して対応した。ビルド・テスト・性能測定・公式スイートの再実行は行っていない。

同じセッションでの1回目の再レビューは終了値0で完了し、初回の採否への異論や要件退行はなかった。担当表に残った不一致と追加Low指摘3件へ、次の文書修正を行った。

- **初回10の残存部分:** 要件2.6・4.1・4.3・5.3・6.7の担当も対応表へ揃え、担当グループにRunManifest・ComparisonPolicy・素材生成scriptを明記した。
- **R2-1: 通常CIとBDN smokeの実行環境（一部採用）。** 別process smokeをTUnitへ実装する場合にUbuntu CIへ混入するという条件付きの懸念であり、現行の実行失敗ではない。通常CIの契約テストは保存データfixtureを使い、BDN smokeはWindows x64での前提確認・受入手順として分けることを明記した。新しい環境抽象やOS対応は追加しない。
- **R2-2: 比較ディレクトリのpath差（採用）。** 復元先と候補側ではroot・cwd等が異なるため、その差を内容・設定差と区別する必要がある。絶対pathを役割・相対pathへ対応付け、source集合のhashは相対pathと内容で算出し、harnessはsource・依存版・ビルド条件で照合する。assembly hashは使用成果物の記録として保持する。PDBによる具体的なhash差の発生は未検証であり、確定事実とはしない。
- **R2-3: harness変更時の再測定（一部採用）。** policy確定後の変更を既存の比較条件差として拒否する契約はあるが、再開手順が不足していた。受入用harnessをprojectと共通コード・設定の単位で凍結し、変更が必要なら基準runtimeと修正済みharnessの組合せで基準値を取り直す。再レビューのR3-2を踏まえ、元のpolicyの目標・許容幅・同等範囲は維持する。追加の原因調査ケースは別のDiagnostic系列に置く。ケース単位の依存解析は導入しない。

再レビューの任意提案である理由enumの導入、共通SIMD補助を個別列挙すること、公式ランナーREADMEのCI説明への追記は、今回の必須修正へ含めない。理由の表現型は実装時に判断し、共通補助への変更は既存の関連仕様・直接テストの選定で扱う。公式ランナーREADMEは同ツールの通常CI範囲を説明しており、独立測定ツールの案内はルートREADMEと測定ツールREADMEが担う。

2回目の再レビューも同じセッションで終了値0を確認した。初回10とR2-1・R2-2は解消し、R2-3への補足段落についてLow指摘2件があった。

- **R3-1: Diagnosticケースとharness凍結の矛盾（採用）。** 同じprojectへ追加すればsource集合が変わるという指摘は妥当である。凍結したprojectを別ディレクトリへ複製し、その診断用コピーの`PreparationBenchmarks`へ追加する手順へ具体化した。受入用source集合は維持し、新しいproject種別は作らない。
- **R3-2: harness変更後のpolicyの後付け（設計補足を採用）。** 変更前に基準を定める要件は共通であり、意図として後付けを許すものではないが、「新しいpolicy」の表現では変更できる範囲が曖昧だった。全体policyを含む元の目標・許容増加幅・同等範囲を保持し、基準側の観測によるノイズ幅と反復間判断幅だけを更新できること、元の条件で判断できなければ受け入れないことを明記した。

採用判定のテスト用入口に関する任意の提案は、実装時に責務を分けて具体化する。文書修正のためだけに新しい抽象や公開契約を追加しない。

## 参照（References）

調査記録の各節に、確認した固定版の一次ソース、公式文書、既存コードとADRを記載した。外部資料の一般的な説明と固定版コードが異なる場合は、採用する固定版の挙動を設計へ反映した。
