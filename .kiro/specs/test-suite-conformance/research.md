# 調査・設計判断: test-suite-conformance

第1〜7節は要件作成後のギャップ分析記録、第8節以降は2026-10-03の技術設計調査と採用判断とする。ギャップ分析時点の未承認状態・候補案と、その後の設計判断を区別する。

## 分析の要約

- 既存の4段階API、型検査、リンク、実行境界と公式ランナーを再利用できる。主な不足は、ランタイムが生成する診断の前方一致、入力に応じた原因選択、原因から修正・受入結果までの追跡である。
- 既知944件は102組の観測診断であり、原因数ではない。UTF-8の528件など共通箇所で扱えるものに加え、LEB128、式終端、検査順序には文言置換では解消できない差がある。
- 保存済み全体baselineと生成素材は現在の環境に存在する。baselineのSHA-256は引継ぎ記録と一致し、比較元の再作成や新しいランナーは現時点で不要である。
- 既存責務を拡張し、必要な範囲だけ診断生成を分離する混合案が有力候補となる。採否、分割単位、検査順序は設計で決める。
- 暫定規模はL、リスクはHighとする。参照decoderとの境界処理の差と複数不正の優先順が主要な不確定要因であり、後続命令の先行実装は含めない。

## 1. 調査条件と証拠の範囲

| 項目 | 確認内容 |
| --- | --- |
| 調査日 | 2026-10-03 |
| 調査対象HEAD | `998bb7036e4d26fe494d4265c46217fe53e32f17` |
| 要件 | [requirements.md](requirements.md)の要件1〜8、計48受入基準 |
| 承認状態 | [spec.json](spec.json)は`requirements-generated`、要件未承認。gap分析は継続するが、設計・実装の承認とは扱わない |
| 方法 | コンテキスト・受入基盤、ランタイムと既存テスト、固定参照実装・依存を3つの独立した読み取り専用調査に分け、結果を統合 |
| 実施した確認 | 現行ソースと既存テストの静的調査、保存済み資料の存在・集計、baselineのhash、固定submoduleとprofileの照合 |
| 実施していない確認 | ビルド、テスト実行、公式スイート再実行、生成物全件のhash再検査、944件すべての根本原因の確定 |

現行の[ロードマップ](../../steering/roadmap.md)と[ランナーの実装記録](../test-suite-runner/tasks.md)では、上流`test-suite-runner`は初回公式受入とbaseline保存済みだが、仕様全体の最終実装検証は未完了である。分析はこの状態を前提とし、上流の完了や本仕様の実装着手を承認しない。[brief.md](brief.md)の「ブリーフ段階」は作成時の記述であり、現在の段階はspec.jsonと要件文書から確認した。

以下の件数は2026-10-01の保存済み結果であり、調査対象HEADでの再実行結果ではない。

| 分類 | 保存済み件数 |
| --- | ---: |
| passed | 1,547 |
| failed | 944 |
| runtime_unsupported | 2,987 |
| runner_error | 0 |
| out_of_scope | 1,077 |
| blocked | 47,352 |
| 合計 | 53,907 |

[診断一覧](diagnostic-groups.json)を解析し、102組、944件、一意なケースID944件、各組の件数合計944を確認した。内訳は`assert_malformed`683件、`assert_invalid`183件、`assert_unlinkable`77件、`assert_uninstantiable`1件である。保存済み`acceptance-audit.json`の必須66ケースは全passedだった。診断文の不一致だけが観測されていても、参照実装と同じ不正を選択した証拠にはならない。

### 比較元と固定素材

- `artifacts/test-suite-runner-acceptance-20261001/`に`run-baseline.json`、`run-moved.json`、`run-diff.json`、`acceptance-audit.json`、`portable/corpus/manifest.json`と`modules/`が存在する。
- baselineのSHA-256は`032cc64b6c088e920331b420490617da6094454ab08a52381ab7c35b1be3c057`で、[引継ぎ文書](handoff.md)と一致した。旧revision`1bda5b9368f04541676213ea90a9418e1f241751`もローカルGitオブジェクトに存在する。
- specは`05ca4182176763112561ae20153975c12bd689e4`、WABTは`03a00a1334e6121fb0cce4fccbd6bb109b68acaa`で、ローカルHEADと親リポジトリのgitlinkが一致した。
- 固定公式WAST147件のhashは[埋込みprofile](../../../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)と一致した。保存済みmanifestは147入力成功・5,821生成物を記録し、modules配下にも5,821ファイルが存在する。ファイル数の確認を生成物全件の内容照合とは扱わない。
- 固定参照実装はローカルに揃っている。依存追加・版更新・外部取得が必要な根拠は見つからなかった。生成済み素材のrunにはWABTや元WASTは不要である。

`artifacts/`はGit管理外である。別環境で資料を取得できない場合は、handoffの旧revisionによる再作成手順と照合限界に従う。診断一覧や修正後の結果を旧全体baselineの代用品にしない。

## 2. 既存資産と責務の接点

| 既存資産 | 再利用する責務・変更時の接点 |
| --- | --- |
| [WasmModule](../../../src/WasmSharp/WasmModule.cs) | Decode・Validate・Instantiateの公開操作。Validateは全体成功後だけ実行コードと成功状態を確定する |
| [ModuleBinaryReader](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs) | 範囲を限定した読取り、厳格UTF-8、LEB128、入力位置。`ReadBytes`・`ReadRange`・`Location`は安全性と位置情報の基盤 |
| [ModuleBinaryFormat](../../../src/WasmSharp/Modules/ModuleBinaryFormat.cs) | header・section・import・型・limitsの符号化と件数。DecodeとInspectImportsが共有する |
| [ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs) | module構造、function/code、locals、式の読取り。未実装機能と未確認範囲を通知する |
| [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs) | 型・添字・構造の検査と、同一パスでの線形実行コード生成 |
| [ModuleInstantiator](../../../src/WasmSharp/Modules/ModuleInstantiator.cs) | importの名前・種類・型照合、共有実体の接続、定義リソースの割当、startの起動。`LinkFailure`がReason・import情報・Locationをまとめる |
| [ExecutionBoundary](../../../src/WasmSharp/Execution/ExecutionBoundary.cs) | 内部のtrap/exhaustion結果から公開例外への共通変換。InvokeとInstantiateの段階を呼出元から受け取る |
| [ImportInspector](../../../src/WasmSharp/Modules/ImportInspector.cs) | module生成をせず完全なimport情報と未確認範囲を返す。共通reader変更の影響先 |
| [AssertionJudge](../../../tools/WasmSharp.TestSuiteRunner/Execution/AssertionJudge.cs) | 既存の段階・例外型・リンクReasonと、加工しない公開MessageのOrdinal前方一致を判定する |
| [ReportStore](../../../tools/WasmSharp.TestSuiteRunner/Reports/ReportStore.cs)、[CompletionPolicy](../../../tools/WasmSharp.TestSuiteRunner/Reports/CompletionPolicy.cs) | 全体結果の記録検証、工程別終了判定。runとCore 2.0全体のverifyを区別する |
| [BaselineComparer](../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineComparer.cs)、[BaselineStore](../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineStore.cs) | 同条件のケース別比較、passedの退行・欠落検出、保存済みJSONの明示保存 |

公開型を増やす必要は現時点で見つかっていない。診断に必要な添字・種類・位置・Reason・import識別情報は大部分が検出箇所に存在する。ランタイムは公式JSON・期待文字列・ケースID・WABTに依存させず、ランナーから公開APIを利用する一方向の依存を維持する。

Core仕様の[embeddingにおけるエラー](../../../thirdParties/WebAssembly-spec/document/core/appendix/embedding.rst)は、具体的な診断文まで一律に定めていない。固定期待診断と原因選択への互換性は[ADR0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)の追加契約であり、一般的なCore 2.0準拠と区別する。参照ランナーの[assert_message](../../../thirdParties/WebAssembly-spec/interpreter/script/run.ml)にも前方一致の検査がある。

## 3. 要件と既存資産の対応

`Missing`は不足している実装・記録、`Unknown`は未確定の原因・影響、`Constraint`は維持すべき契約を示す。既存能力があることと、修正後の受入が完了したことは区別する。

| 受入基準 | 既存能力 | ギャップ・制約 |
| --- | --- | --- |
| 1.1 | CaseResult・診断一覧に入力path、command index、期待・実際、段階、公開診断を保存 | **Missing:**新たな不一致も含めた原因・修正・確認方法との対応記録 |
| 1.2、1.5 | 固定仕様・参照実装・公式入力と102組の観測が存在 | **Unknown:**根本原因の単位。**Missing:**ケース→仕様規則→原因→修正→確認結果の追跡 |
| 1.3〜1.4 | handoffとroadmapに上流・後続仕様との境界を定義 | **Missing:**再実行で追加された不一致と引継ぎの記録。**Constraint:**分類変更による解消を認めない |
| 2.1〜2.2 | 値表現、関数・リソース共有、定義リソースのinstance独立性と直接テスト | **Constraint:**修正中も維持。**Unknown:**再実行で新たに見える動作・値・状態の不一致 |
| 2.3〜2.5 | 段階別例外、内部実行結果、ホストcallback例外の実体を保つ伝播 | **Constraint:**未実装・ホスト例外・一般資源不足・実装上限をWasmの期待失敗へ包み直さない |
| 2.6 | WasmModule.Validateの成功時確定、RequireValidatedによるInstantiate拒否 | **Constraint:**検査順序の変更でも部分成果を検証成功状態へ公開しない |
| 2.7 | ImportInspectorの完全一覧と未確認範囲、失敗時の部分一覧非公開 | **Constraint:**共通reader変更後も調査成功とmodule全体の検証成功を分ける |
| 3.1〜3.3 | ランナー側は加工しないOrdinal前方一致を実装済み | **Missing:**ランタイム生成診断の公式prefixと、その後ろに置く補助説明 |
| 3.4 | Decode・Validate・Linkそれぞれに明確な検査順がある | **Unknown:**複数不正時に選ぶ原因と固定参照実装との差。代表的な順序差は第4節参照 |
| 3.5〜3.6 | Location、LinkFailure、ThrowIfFailedと既存の構造化情報 | **Constraint:**選んだ原因から診断と情報を生成。公式素材へのランタイム依存を導入しない |
| 4.1 | ReadNameの厳格UTF-8検査 | **Missing:**`malformed UTF-8 encoding`のprefix。観測528件 |
| 4.2 | ReadIntegerの最大幅・未使用ビット検査 | **Missing:**表現長超過と値範囲違反の区別。**Unknown:**両者や入力終端が重なる場合の優先順 |
| 4.3 | ReadHeaderの8バイト検査 | **Missing:**magic・version・途中EOFの分離。早期不一致と入力不足の選択も照合する |
| 4.4 | section・kind・mutability・limits・vector・locals・function/codeの検査 | **Missing:**対応する診断。**Unknown:**1byte検査と参照側の整数符号化検査、件数検査の優先順 |
| 4.5〜4.6 | 限定reader、式のend検査、RequireEnd | **Missing:**終端・境界の診断の区別。**Constraint:**入力とsectionの範囲検査を保持し、不正な長さを受け入れない |
| 5.1 | 型stack、関数結果、initializer結果の検査 | **Missing:**`type mismatch`のprefix。**Unknown:**定数式制約と結果型・個数の不正が併存する場合の選択 |
| 5.2〜5.3 | 種類別添字とexport名重複を検査 | **Missing:**種類と必要な数値を含む`unknown`、`duplicate export name`の診断 |
| 5.4 | 定数式、global可変性、start型の検査 | **Missing:**`constant expression required`、`global is immutable`、`start function`の診断 |
| 5.5 | limitsとmemory数・ページ上限の検査 | **Missing:**上限超過とmin/max逆転の診断分離。**Unknown:**複数違反時の選択 |
| 6.1〜6.2 | LinkのMissingImport・KindMismatch・TypeMismatch、import識別情報 | **Missing:**`unknown import`と`incompatible import type`のprefix。**Constraint:**同じprefixでもReasonを統合しない |
| 6.3〜6.4 | start/Invoke共通の実行境界とtrap/exhaustionの区別 | **Missing:**既存経路のReasonに対応する診断。**Unknown:**公式ケースの到達可能範囲。後続命令は前倒ししない |
| 6.5 | start前の構築・接続、失敗時に参照や副作用を取り消さない処理とテスト | **Constraint:**診断生成・順序変更によって参照失効やrollbackを導入しない |
| 7.1〜7.2 | 固定profile・全体run・ReportStoreの欠落/重複/未処理/未確定検出 | **Missing:**修正後の全147入力・53,907commandの完了結果。新ランナーは不要 |
| 7.3 | 既知944件の一意IDとケース別比較結果 | **Missing:**全944件それぞれが診断照合込みでpassedになった確認記録 |
| 7.4〜7.5 | failed・入力/commandのrunner_errorを非0にする既存判定 | **Missing:**全体のfailed/runner_errorゼロ。**Constraint:**公式入力・期待値・profile・判定条件を維持 |
| 7.6 | CaseDiagnostic.Feature/UnverifiedRanges、CaseCause.Direct/Origins | **Unknown:**残る未対応が後続の新規機能だけか。**Missing:**機能・原因command・所有仕様の対応確認 |
| 7.7〜7.8 | run/compare-run/verifyの別判定、固定規則とケースの出典 | **Missing:**run/compare-run終了0と規則・診断選択・検証結果の対応。verify非0とは区別 |
| 8.1 | 引継ぎbaselineが存在しhash一致、消失時の旧revision再作成手順 | 現在の比較元入手に不足なし。**Constraint:**診断一覧・修正後結果で代用しない |
| 8.2〜8.3 | profile・入力/生成物hash照合、passedの退行・欠落を回帰として検出 | **Missing:**修正後の比較結果。既存1,547passedと必須66ケースを維持 |
| 8.4〜8.6 | 比較と保存が別操作、保存済みJSONだけをbaseline-saveへ渡す | **Missing:**受入条件成立後の明示保存と引継ぎ記録。**Constraint:**保存成功を公式合格と扱わない |

## 4. 代表的な差分と統合上の難所

### 4.1 Decodeは診断文と読取り条件の両方を扱う

| 観測・ソース | 現在と固定参照実装の差 | 設計で確認する内容 |
| --- | --- | --- |
| D034、ReadName | 現在は日本語Message。参照側は長さ・バイト列の取得後にUTF-8を検査 | 厳格UTF-8と長さ検査を保ち、ランタイム生成箇所でprefixを付ける |
| D027/D030、ReadInteger | 現在は最終byteの継続bitと不正payloadを同じ分岐へ集約 | 参照`uN`/`sN`はpayload検査後、継続先で残り幅を確認する。両不正が重なる場合も含めて分ける |
| D028/D031/D029、ReadLimits/ReadTypes | 現在はflag・型形式を1byteで照合。参照側は`u1`/`s7`の整数読取りを使う | 既存の拒否条件を弱めず、整数符号化違反を先に選ぶ条件を特定する |
| D033/D042/D043/D047、ReadHeader | 現在は8byteの逐次比較でmagic/version共通文言 | 固定参照側のmagicとversionの読取単位、途中EOFと不正byteの優先順を照合する |
| D024/D039/D045、ReadInstructions | 同じ「式のendがありません。」に3つの公式期待診断が対応 | 後続byteと入力EOF、宣言されたbody/section境界を区別して原因を選ぶ |
| D026/D032/D044/D046、ReadBytes/ReadCount | 現在の残量検査は複数の構文エラーに先行し得る | 長さの範囲外、整数表現長、section/function終端を文脈に応じて区別する |

固定[decode.ml](../../../thirdParties/WebAssembly-spec/interpreter/binary/decode.ml)の83〜99行は整数の検査順、135〜140行の`sized`は同一stream上で内容を読んだ後のsize一致検査、780〜791行は命令列とENDの分離を示す。現在の`ReadRange`はsectionとbodyを限定した子readerを作り、`ReadInstructions`はその境界で停止するため、構造が異なる。

固定[binary.wast](../../../thirdParties/WebAssembly-spec/test/core/binary.wast)の416〜470行と診断一覧の`binary.wast#76/#77/#78`には、END欠落でも後続byteにより`END opcode expected`、`unexpected end of section or function`、`section size mismatch`が分かれる具体例がある。単一のMessage変換表では入力条件を復元できない。安全性を保つreaderの責務と、診断選択に必要な入力文脈の持たせ方を先に検討する。参照実装をそのまま移植して物理入力の範囲外読取りを許す案にはしない。

### 4.2 Validateは既存の検査を再利用し、原因の選択を調べる

現在の`ModuleValidator.Validate`は、全体参照→global初期化式→start→関数本体の順である。一方、固定[valid.ml](../../../thirdParties/WebAssembly-spec/interpreter/valid/valid.ml)の`check_module`は、コンテキスト構築後に型、global、table、memory、segment、関数本体、start、export、memory数の順に検査する。順序差は確認できるが、すべてが既知944件の原因であるとは断定しない。

個別の差もある。現在のinitializerは命令数を先に検査するが、参照`check_const`は定数式として許される命令かを先に調べる。現在の`ValidateLimits`は仕様上限とmin/max逆転を同じ条件へまとめるが、参照`check_limits`は最小・最大の上限を先に、min≤maxを後に調べる。export添字、function/local/global添字は種類や数値を既に保持しているため、診断のためだけの公開API追加は不要である。

検査を整理する場合も、型検査と線形化の同一パス、および`WasmModule.Validate`の全体成功時だけの状態確定を維持する。根拠は[ADR0002](../../../docs/adr/0002-single-pass-linear-interpreter.md)と[ADR0005](../../../docs/adr/0005-module-owned-validation-state.md)である。

### 4.3 リンクと実行境界には既存の集約箇所がある

`ModuleInstantiator.LinkFailure`はReason・import ordinal・名前・要求種類・Locationを既に持つ。MissingImportは`unknown import`、KindMismatchとTypeMismatchは`incompatible import type`というprefixを生成する候補箇所になる。同じprefixでもReasonは区別する。

現在のLinkはimport宣言ごとに名前→種類→型を検査する。固定参照ランナーは[Import.link](../../../thirdParties/WebAssembly-spec/interpreter/script/import.ml)で名前を解決した後に[Eval.init](../../../thirdParties/WebAssembly-spec/interpreter/exec/eval.ml)で型を照合する。名前不足と型不一致が別の宣言に併存する場合の選択はResearch Neededとする。

`ExecutionBoundary.ThrowIfFailed`はtrap/exhaustionに現在それぞれ共通の日本語Messageを付けている。固定参照側には`unreachable executed`や`call stack exhausted`があり、公式期待prefixに合わせる際も既存Reason、上限、元位置、Invoke/Instantiateの段階を保つ。現在failedのない`assert_trap`/`assert_exhaustion`を診断互換性の完成済みとは扱わず、先行機能で到達できる範囲を調べる。未実装trap命令の追加は後続仕様へ残す。

公開例外constructorは任意のMessageを保持する契約があり、[constructorテスト](../../../tests/WasmSharp.Tests/Exceptions/WasmException_ConstructorTests.cs)でも確認されている。constructor全体で文言を付け替えず、ランタイム自身の診断生成箇所を対象にする。ホストがWasm例外型を投げた場合も型・実体を保つ[ADR0007](../../../docs/adr/0007-propagate-host-exceptions.md)、共通実行境界の[ADR0004](../../../docs/adr/0004-trap-result-propagation.md)、start失敗後の参照保持の[ADR0011](../../../docs/adr/0011-retain-references-after-start-failure.md)を維持する。

### 4.4 受入の不足はランナー能力と区別する

`CompletionPolicy.Run`はblockedの起点がruntime_unsupportedであることを確認するが、その機能がどの後続仕様に属するかまでは判断しない。要件7.6には、結果のFeature・未確認範囲・原因commandから所有仕様を確認する記録が必要である。

`BaselineComparer`は以前passedだったケースの退行・欠落を検出するが、既知failed944件がすべてpassedになったことを専用条件としては扱わない。要件7.3は診断一覧の944個のIDと修正後結果を照合する。`baseline-save`も完全な結果の保存可否を判定する操作であり、要件8.4の受入成立後だけの更新は担当者の手順として維持する。これらを本仕様のための判定機能追加へ置き換えない。

## 5. 実装方式の選択肢

どの案でも公開API、ランナーの判定条件、固定素材、後続仕様との境界を保つ。必要な責務分離を先に行い、その後に診断・検査動作を変更する。

| 案 | 具体的な変更候補 | 利点 | 費用・リスク |
| --- | --- | --- | --- |
| A:既存コンポーネントを拡張 | reader/format/decoder/validator内の分岐と診断を修正し、LinkFailureとThrowIfFailedを拡張 | 新規型が少なく、検出箇所にある情報を直接使える。局所的な修正から進めやすい | decoder/validatorに選択処理が集中する。同じprefix生成が重複しやすく、境界条件の見通しに注意が必要 |
| B:段階ごとの内部診断部品を追加 | Modules・Executionの既存責務内に、検出条件・添字・位置から既存例外を作る小さな部品を置く | 文言と構造化情報の整合をまとめて確認できる。複数箇所で使う生成規則を共有できる | 読取順・検査順の変更は別に必要。全エラー共通frameworkや新しい公開Reason体系へ広げると過剰になる |
| C:既存拡張と必要箇所の分離を組み合わせる | 原因選択は既存reader/decoder/validatorで調整し、重複が確認できた診断生成だけを分離。リンク・実行は既存の集約箇所を使う | 現在の情報の所在と責務に沿い、公開API・単一検証パス・共通実行境界を維持しやすい | 分離する単位の判断が必要。分散した文言生成と新部品の役割が重ならないよう設計する |

設計へ持ち込む有力候補はCとするが、最終選択ではない。原因が単一箇所に閉じるものはAで十分であり、Bは複数箇所で共有する診断規則が確認できた場合に限定する。専用ランナー、公式ケース別の変換表、実行時の期待値参照はどの案にも含めない。

原因の記録には、既存Markdownへ追記する案と、ケースID・原因IDの機械可読な対応資料を併用する案がある。前者は簡潔、後者は944件の照合に向く。後者を選ぶ場合も、既存結果JSONへの参照を中心にし、別baselineや別の判定エンジンは作らない。102組の観測IDと原因IDは分ける。

### 規模とリスク

- **Effort: L（1〜2週間相当、暫定）**。4段階と公式実行基盤は再利用できるが、複数の解析・検証分岐の整理、原因別回帰、全体再実行と追跡が必要である。944件を944個の修正とは見積もらない。
- **Risk: High**。安全な読取境界と参照診断の選択の両立、複数不正の検査順、修正後に露出する不一致が未確定である。原因調査で広い構造変更が必要と判明した場合は規模を見直す。
- 新規外部依存や配備作業は見込まない。診断Messageの外部観測は意図して変わるが、公開型・引数・Reason・Location・共有状態を不要に変更しない。

## 6. 設計へ引き継ぐ調査事項（Research Needed）

| ID | 調査事項 | 設計に必要な成果 |
| --- | --- | --- |
| R1 | 診断一覧の各ケースと固定入力・規則・参照実装を照合する | 102組とは別の根本原因単位、ケースから原因・修正候補・確認方法への対応 |
| R2 | EOF、宣言section/body境界、後続byte、長さ検査の関係 | 範囲安全性を維持しながら期待診断と整合したLocationを選べる内部責務と、代表的な複合不正の確認例 |
| R3 | LEB128の幅・payload・継続bit、u1/s7、header読取単位 | 診断分岐と優先順を入力条件で説明できる規則。単純な継続bit優先を仮定しない |
| R4 | validator全体、定数式、limits、export、importの複数不正 | 必要な検査順変更と影響範囲。参照側のfold方向や名前解決/型照合の分離も具体例で確かめる |
| R5 | 診断生成の配置と構造化情報の保持 | A/B/Cの採否、共通化する実際の重複、公開constructorとホスト例外を変更対象外に保つ方法 |
| R6 | 残るruntime_unsupported/blockedとtrap/exhaustionの到達可能性 | 未実装Feature→所有仕様、blocked→原因commandの対応。先行機能の不具合を未対応へ隠さない確認 |
| R7 | 修正・受入の記録形式 | 944件の個別passed化、新規不一致、全体比較、baseline更新の証拠を既存JSONへ対応付ける最小限の方法 |

深い全件原因調査と方式の確定は設計段階へ残す。未承認要件の変更が必要になった場合も、調査結果と提案を区別して扱う。

## 7. 検証資産と実装後の受入方針

既存テストは例外型・位置・Reason・状態・同一性を保つ回帰に利用できる。今回確認したランタイムテストのMessage assertionは主にconstructorへ渡した文言の保持であり、公式prefixと原因選択には追加確認が必要である。全件分の重複した単体テストを作らず、診断生成規則と順序を変える境界を中心に既存テストを拡張する。

2026-10-03の要件検討で、診断選択の保証範囲を[要件文書](requirements.md#対象範囲と隣接仕様)のとおり確認した。上記のテストでは、固定公式ケースと固定版の参照実装から確認した規則が通常入力にも共通して適用されることを確かめる。任意入力のあらゆる不正の組合せを網羅する確認は、本仕様の受入に要求しない。

| 確認対象 | 既存テストの入口 |
| --- | --- |
| UTF-8・LEB・範囲・Location | [ModuleBinaryReaderTests](../../../tests/WasmSharp.Tests/Modules/ModuleBinaryReaderTests.cs)、[ModuleDecoder_DecodeFailureTests](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_DecodeFailureTests.cs) |
| 型・添字・limits・検証失敗後の状態 | [ModuleValidator_ValidateExternalsTests](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateExternalsTests.cs)、[WasmModule_ValidateTests](../../../tests/WasmSharp.Tests/WasmModule_ValidateTests.cs) |
| importの照合と情報、共有と独立性 | [WasmModule_InstantiateLinkingContractTests](../../../tests/WasmSharp.Tests/WasmModule_InstantiateLinkingContractTests.cs)、[WasmModule_InstantiateLinkingTests](../../../tests/WasmSharp.Tests/WasmModule_InstantiateLinkingTests.cs) |
| startの段階・保存参照・副作用 | [WasmModule_InstantiateStartTests](../../../tests/WasmSharp.Tests/WasmModule_InstantiateStartTests.cs) |
| trap/exhaustion・ホスト例外・上限 | [ExecutionBoundary_ThrowIfFailedTests](../../../tests/WasmSharp.Tests/Execution/ExecutionBoundary_ThrowIfFailedTests.cs)、[WasmFunction_InvokeExhaustionTests](../../../tests/WasmSharp.Tests/WasmFunction_InvokeExhaustionTests.cs) |
| import調査の独立性と未確認範囲 | [WasmModule_InspectImportsTests](../../../tests/WasmSharp.Tests/WasmModule_InspectImportsTests.cs) |
| 公式判定・終了値・回帰・保存 | [AssertionJudge_JudgeTests](../../../tests/WasmSharp.TestSuiteRunner.Tests/Execution/AssertionJudge_JudgeTests.cs)、[CompletionPolicy_RunTests](../../../tests/WasmSharp.TestSuiteRunner.Tests/Reports/CompletionPolicy_RunTests.cs)、[BaselineComparer_CompareRunTests](../../../tests/WasmSharp.TestSuiteRunner.Tests/Baselines/BaselineComparer_CompareRunTests.cs)、[BaselineStore_SaveTests](../../../tests/WasmSharp.TestSuiteRunner.Tests/Baselines/BaselineStore_SaveTests.cs) |

既存AssertionJudgeがReasonを判定に使うのはunlinkableの許容Reasonであり、trap/exhaustionのReason・Location・上限の整合まで公式prefixだけでは証明できない。これらは直接テストと規則への対応で確認する。共通readerを修正する場合はInspectImportsも回帰対象になる。

実装後は次の順序を既存の[ランナーガイド](../../../tools/WasmSharp.TestSuiteRunner/README.md)と[引継ぎ手順](handoff.md)で実施する。ここでは実施結果を記録したものではない。

1. 必要な責務整理を先に行い、原因単位で修正する。変更したテストは、警告・エラー0のReleaseビルドを確認してからコマンドで実行する。公式受入前には既存の通常検証を完了する。
2. 修正したrevisionの通常ビルド出力を使い、固定manifestで全147入力・53,907commandをrunする。移動受入用に保存された旧runnerを使わない。
3. 現baselineとcompare-runし、記録の完全性、以前のpassedの維持、failedと入力/commandのrunner_errorゼロ、比較成立・完了、回帰ゼロを確認する。
4. 既知944件のIDを修正後結果へ照合し、それぞれのpassed化を確認する。新たな不一致、未対応Featureと所有仕様、blockedの原因を別途追跡する。
5. 不一致が残れば修正・全体再実行・比較を繰り返す。完了条件成立後だけ保存済みJSONをbaseline-saveで明示更新し、結果と比較記録を引き継ぐ。
6. 本仕様のrun/compare-run終了0と、未対応・blockedが残る間のverify非0を区別する。個別テストや保存成功でCore 2.0全体の合格を代替しない。

本分析は実装方式を比較するための調査記録であり、修正完了・公式受入・仕様承認を宣言するものではない。次は本書の未確定事項を入力として`kiro-spec-design test-suite-conformance`で設計を作成する。

## 8. 技術設計調査の要約（Summary）

- **対象:** test-suite-conformance。ユーザー指定の`kiro-spec-design test-suite-conformance -y`により要件承認を進め、設計を生成する。設計自体の承認と実装着手は別に扱う。
- **調査区分:** 既存機能の拡張に対する統合中心のlight discovery。新規ライブラリや外部サービスは追加せず、難所であるバイナリ境界・原因選択を固定ソースから詳しく調査した。
- **現在の根拠:** HEADは`998bb7036e4d26fe494d4265c46217fe53e32f17`。現在の要件、ADR0012、core steering、roadmap、既存ランタイム・ランナー・テスト、固定参照ソースを確認した。過去の記憶にある上流完了状態は採用せず、現行roadmapの最終実装検証待ちを前提とする。
- **主要な発見:** 限定readerと参照decoderの差はENDだけでなくLEB128・長さにも影響する。ValidatorとLinkには走査順の差がある。既存ランナーに不足するのは本仕様固有の原因追跡と受入監査記録であり、新しい比較機能ではない。
- **確認範囲:** ソースの静的調査と保存済みJSONの再集計・hash照合。ランタイム修正、ビルド、テスト、公式再実行、944件すべての修正後成立は実施していない。

参照したスキルは[kiro-spec-design](../../../.agents/skills/kiro-spec-design/SKILL.md)、[codebase-design](../../../.agents/skills/codebase-design/SKILL.md)、ホストの`C:/Users/taihe/.codex/skills/csharp-conventions/SKILL.md`。順に、境界を先に確定する設計・レビューゲート、小さいInterfaceに処理を隠すModule、既存C#・TUnit・配置規則を適用した。調査をDecode、Validate・Link・実行境界、公式受入の3つの読み取り専用調査に分け、方式の統合と簡素化は主担当が行った。

## 9. 設計調査記録（Research Log）

### 9.1 宣言範囲と物理入力を区別する必要がある

**背景:** 同じ現在のMessageに異なる期待診断が対応するため、文言変換表では解消できない。

**参照:** [reader](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs)、[decoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs)、[固定decode.ml](../../../thirdParties/WebAssembly-spec/interpreter/binary/decode.ml)、[binary.wast](../../../thirdParties/WebAssembly-spec/test/core/binary.wast)、[binary-leb128.wast](../../../thirdParties/WebAssembly-spec/test/core/binary-leb128.wast)。外部の一次資料として同じcommitの[decode.ml](https://raw.githubusercontent.com/WebAssembly/spec/05ca4182176763112561ae20153975c12bd689e4/interpreter/binary/decode.ml)も参照した。最新mainや別版の診断は使用していない。

| 観測ケース | 固定参照の選択規則 | 設計への影響 |
| --- | --- | --- |
| binary.wast#76 | body直後の0x05で命令列を終了し、END要求に失敗 | 命令列とEND要求を分ける |
| binary.wast#77 | body末尾が物理EOFでENDを取得できない | EOFと宣言境界を区別する |
| binary.wast#78 | 次sectionの0x0BをENDとして読んだ後、消費位置が宣言長と異なる | 診断選択だけで後続byteを参照し、入力を受理しない |
| binary-leb128.wast#36 | 宣言sectionを越えるLEBの表現長不正が先に判明する | ENDの1byte先読みだけでは不足 |
| binary.wast#162 | 次sectionのbyteを名前長として解釈し、物理残量に対する長さ超過を検出 | 長さの規則も同じ解析で再走査する |
| binary.wast#148/#153 | count=1と残byte=0でも長さ取得は成立し、要素取得でEOF | prefix読取り前の位置を長さ上限の基準にする |

LEBは残り幅確認→byte取得→payload確認→継続の順であり、最終payload不正と継続bitが両方ある場合は`integer too large`が先になる。幅を使い切った合法payloadの継続は、次byteがなくても`integer representation too long`になる。limits flagはu1、function/value/reference typeはs7だが、mutabilityとimport/export kindは生のbyteである。

headerは4byteのmagic取得・照合後に4byteのversionを取得・照合する。先頭byteが不正でも4byte揃う前のEOFが優先される。現在の位置契約を不要に変えず、逐次byte取得と最初の相違位置の情報を保ち、比較の時機だけを変更する。

custom payloadは宣言範囲の残量だけを消費し、診断モードの物理残量をすべて読み飛ばしてはならない。custom.wast#5のように名前取得後の宣言残量が負になる場合、固定decode.mlのcustom→get_string→skipはEOSを通知し、guardが`unexpected end of section or function`へ変換する。sizedの`section size mismatch`より先に確定する診断である。localsは全圧縮宣言の構文を読んだ後に合計のu32上限を検査する。sectionの逆順・重複は、そのpayloadを読まずに診断する。

**採用への影響:** 正常な限定readerを保ち、確定した境界失敗だけを内部通知としてDecode入口へ戻す。同じ構文解析を診断モードで最大1回呼ぶ。診断モードも物理入力内のspanを使用し、宣言長との一致を必須にする。正常結果・未対応・実装上限で止まった場合は元の範囲不正を返す。後続命令のdecoderや別の命令表を追加しない。

### 9.2 原因選択と公開情報を同じ検出箇所に結び付ける

**参照:** [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs)、[ModuleInstantiator](../../../src/WasmSharp/Modules/ModuleInstantiator.cs)、[ExecutionBoundary](../../../src/WasmSharp/Execution/ExecutionBoundary.cs)、固定[valid.ml](../../../thirdParties/WebAssembly-spec/interpreter/valid/valid.ml)、[import.ml](../../../thirdParties/WebAssembly-spec/interpreter/script/import.ml)、[eval.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/eval.ml)。同じcommitの[valid.ml](https://raw.githubusercontent.com/WebAssembly/spec/05ca4182176763112561ae20153975c12bd689e4/interpreter/valid/valid.ml)も一次資料として参照した。

- import静的検査はfold_rightによる末尾→先頭、定義関数の型参照は宣言順。その後はglobal→table→memory→関数本体→start→export→memory個数となる。配列・添字空間自体を逆転しない。
- 定数式は命令の適格性を左から短絡判定してから型・個数を調べる。global.getはimportされたglobalの存在を先に確認し、mutableなら定数式違反となる。
- limitsは最小値上限、最大値上限、大小関係の順。exportは添字、重複名の順。memory個数の検査を最後へ移しても、現在の2個目の宣言Locationを保持できる。
- Linkはimport.mlで全名前を宣言順に解決した後、eval.mlのfold_right2で末尾から型照合する。名前不足は先行する別宣言の型不一致より優先し、名前が揃えば最後の不適合宣言を選ぶ。
- LinkFailureにReasonとimport情報、ExecutionBoundaryにReason・Limit・元位置が既にある。公開constructorやホスト例外を加工せず、これらの生成箇所でprefixを決められる。

**採用への影響:** 検査と名前解決の責務を先に分離し、その後に順序を変更する。型検査と線形化の同一パス、全体成功時だけの状態確定、start失敗後の保存参照と副作用、ホスト例外の同一実体を維持する。繰り返す型・添字診断はValidator内のprivate補助処理で十分であり、別の診断サービスは追加しない。

これらは固定ソースで確認した規則であり、既知183件のValidate・77件のLinkすべてについて複合不正の有無を確定したことや、順序変更後のpassedを示すものではない。

### 9.3 後続機能を先行実装しない診断範囲

function/code件数の検査を参照実装どおり一律にmodule末尾へ移すと、[custom.wast](../../../thirdParties/WebAssembly-spec/test/core/custom.wast)の#8では未実装i32.addへ先に到達し得る。既知の件数不一致を未対応へ移さず、宣言件数から確定する既存検査を維持する。通常の未対応を無視して解析を進めたり、診断のために後続命令を実装したりしない。先行機能内で確認した原因選択の規則と、任意入力への完全互換は区別する。

ExecutionBoundaryで現在生成されるtrapはUnreachable、exhaustionはCallDepthLimitとHostStackLimitである。前者を`unreachable executed`、後者を`call stack exhausted`から始める。他のReasonは既存の伝播契約を維持し、その命令の意味論と診断は後続が追加する。

保存済み結果のassert_trap2,408件とassert_exhaustion15件はすべてblockedである。既知failed0を診断互換性の公式確認済みとは扱わず、共通境界は直接APIテスト、到達に新機能が必要な公式ケースは後続仕様で確認する。

### 9.4 受入基盤の再確認

**参照:** [handoff](handoff.md)、[診断一覧](diagnostic-groups.json)、保存済み`artifacts/test-suite-runner-acceptance-20261001/run-baseline.json`、既存[CompletionPolicy](../../../tools/WasmSharp.TestSuiteRunner/Reports/CompletionPolicy.cs)、[BaselineComparer](../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineComparer.cs)、[BaselineStore](../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineStore.cs)。

2026-10-03の設計調査で保存済みbaselineを再読し、SHA-256が`032cc64b6c088e920331b420490617da6094454ab08a52381ab7c35b1be3c057`でhandoffと一致することを確認した。147入力・53,907件のID重複0、診断一覧944IDの一意性とbaseline.failedへの対応、必須66件のpassedも確認した。これらは保存済み資料の検査であり、HEADの再実行結果ではない。

未対応2,987件はFeature400種で、すべてDecode、FeatureとUnverifiedRangesの空記録は0。blocked47,352件のOriginsは641起点で、全起点が同じ入力内の先行runtime_unsupportedだった。起点の欠落・入力違い・逆向き参照は0。Directが中間blockedを指す例もあり、Directだけで所有先を決めない。

| 最初の未実装機能の所有先候補 | runtime_unsupported | 依存blocked |
| --- | ---: | ---: |
| numeric-control | 965 | 13,854 |
| linear-memory | 588 | 5,330 |
| tables-references | 370 | 4,067 |
| simd | 1,064 | 24,101 |

これは調査時の集約候補であり、受入時の固定件数や永久allowlistにはしない。Featureの`select`にはopcode0x1Bと0x1Cが混在し、select.wast#121は0x1Cでi32結果型2個の不正を含む。Feature名だけでは所有境界と入力の有効性を確定できず、曖昧なものはLocationと生成moduleから規則を追う。

既存Runはfailed・入力/commandのrunner_errorと、未対応以外を起点とするblockedを拒否する。CompareRunは旧passedの退行・欠落と比較未完了も拒否する。944件の個別passed化と後続仕様の所有先確認は専用条件に含まれないため、保存済みJSONを結合した本仕様の監査記録で補う。baseline-saveは合格を保証しない。RunProvenanceにGit revisionがないため、受入記録にrevision・未コミット差分識別を追加する。

## 10. 構成案の評価（Architecture Pattern Evaluation）

| 案 | 利点 | 費用・限界 | 結論 |
| --- | --- | --- | --- |
| 各分岐で文言だけ置換 | 変更量が小さい | LEB・END・原因選択が解消しない | 不採用 |
| 原入力上の単一readerへ全面変更 | 参照decoderへ構造を近づけやすい | 通常の限定読取りとInspectImportsの契約への影響が広い | 不採用 |
| 境界ごとのlookaheadや診断専用parser | 正常経路を維持できる | END以外のLEB・長さへ場当たり的に拡大し、解析規則が重複 | 不採用 |
| 限定読取りと失敗時だけの共有解析 | 通常の範囲を維持し、複数の境界診断を同じ構文から決定 | モード管理と失敗時の1回の追加解析が必要 | 採用 |
| 全段階共通の診断サービス | 一覧性 | 原因の検出・位置から離れ、不要な型と依存を増やす | 不採用 |
| 既存責務内の補助処理 | 検出原因と情報が同居し、公開APIを維持 | 段階をまたぐ一括置換はできない | 採用 |

## 11. 設計判断（Design Decisions）

### 決定: 境界の不正と診断選択を分ける

- **一般化:** END、LEB128、長さの違いは、限定範囲での失敗と物理入力上の参照診断選択の差としてまとめられる。
- **採用:** ModuleReadModeとModuleReadBoundaryExceptionを追加し、ModuleDecoder内で同じDecodeCoreを再利用する。診断モードは公開せず、正常・未対応へ分類を変えない。
- **トレードオフ:** 正常経路にもモード・境界情報の保持と分岐は増える。境界失敗時は最大1回の追加走査と一時割当を受け入れる。正常入力を二重解析しない。
- **確認:** 範囲外アクセス、customの読み過ぎ、再走査から正常moduleを公開する経路、内部例外の漏出、未対応への変更を境界テストと全体runで確認する。

### 決定: 診断生成は既存の責務へ置く

- **構築と再利用:** span・厳格UTF-8・既存例外・単一の命令情報・LinkFailure・ExecutionBoundary・公式ランナーを再利用する。別エンジンや外部のWasm診断libraryは、固定入力と現在の構造化情報を同じ検出原因で扱う契約に合わないため採用しない。
- **簡素化:** 新しい公開Reason、汎用診断enum、全段階factory、ModuleValidationDiagnosticsという別クラスは追加しない。重複する型・添字診断は既存Validator内のprivate補助処理へ集約する。
- **確認:** 公開constructorの任意Messageとホスト例外を保ち、補助説明をprefixの後ろへ置く。Reason・Location・import情報・Limitを同時に検査する。

### 決定: 修正追跡と受入監査を作業記録に限定する

- **採用:** remediation.json、acceptance.md、Git管理外のacceptance-audit.jsonで、ケース→原因→修正→検証を対応付ける。公式期待・観測本文は既存CaseResultを参照する。
- **不採用:** ランナーへ944件専用の判定を追加する案、別baseline、恒久的な監査framework、各ケースの期待値を重複保持する案。
- **確認:** 全944件、既存1,547passed、必須66件、全未対応・blockedをケースごとに照合し、新しい不一致も追加する。受入成立まで現baselineを変更せず、成立後の保存済みJSONだけを明示保存する。

## 12. リスクと対処（Risks & Mitigations）

- **再走査の境界情報が不整合:** 物理spanとDeclaredEndを分け、RequireEnd、custom残量、親位置の先送りを契約化する。通常読取りと診断走査を別実装にしない。
- **参照順序の一律移植で未対応に隠れる:** 既知件数不一致を維持し、後続命令を先行実装しない。既知944件と旧passedの個別照合で分類変更を検出する。
- **原因の変更で公開情報が矛盾:** 選択原因からprefixと構造化情報を同時生成し、直接テストで同時確認する。
- **新しい不一致が露出:** 944件を固定の修正上限とせず、同じ責務の修正記録へ追加する。全体run・比較・監査を繰り返す。
- **未対応の所有先が曖昧:** FeatureだけでなくCaseId・Location・opcode・Direct/Originsを調べ、先行機能の不具合を後続へ隠さない。
- **ローカル成果物の消失:** baselineのhashと旧revision再作成手順を保持する。修正後結果を修正前baselineの代用にしない。

## 13. 設計レビューの記録

確定前のドラフトを対象に、要件48件の全対応、責務境界の4項目、ファイル計画、既存ファイルの存在、placeholderなしを機械確認した。research内の相対リンクと行末空白も確認した。

履歴を引き継がない独立エージェントが、固定decode.ml・valid.ml・import.ml・eval.ml、要件、現行コードと設計を照合し、設計ゲートをPASSと判定した。局所的な明確化として、Diagnosticでは内部境界通知を生成せず、物理EOFとRequireEnd失敗もWasmDecodeExceptionへ確定する旨を追記した。研究記録の追記と明確化後に同じエージェントで再確認し、最終PASS、未解決指摘なしを得た。

要件本文、既存ADR0012、初回handoffとdiagnostic-groups.jsonのhashが作業開始時から変わっていないことを確認した。設計ゲートは実装検証・公式受入・ユーザーによる設計承認とは別の判定である。今回の変更はdesign.md、research.md、spec.jsonに限定し、ビルド・テスト・公式スイートは実行していない。
