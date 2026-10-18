# SIMD受入記録

## 1.1 固定素材・比較元と既存fixtureの確認

### Task Brief

| 項目 | 内容 |
| --- | --- |
| 受入基準 | 要件8.1・8.3に従い、SIMDをONにした固定Core 2.0のprofile・公式素材と直前の受入済みbaselineを特定する。既存fixtureの能力、Releaseビルドと3つの既存テストプロジェクト、公式CLIの起動を確認する。 |
| 完了条件 | 本記録と[専用出力先](../../../artifacts/simd/task-1.1-20261018T115544Z/)に、比較元、素材監査、各コマンドのUTC・終了値・ログ行数、テスト件数を保存する。 |
| 設計制約 | 設計の「ファイル構成計画（File Structure Plan）」「ConformanceVerification」「検証方針（Testing Strategy）」に従う。既存CLIとバイナリfixtureを利用し、固定素材・profile・baselineを保持する。 |
| 検証方法 | 入力・素材の存在とSHA-256、固定commit、profileとbaseline内のcorpus、公式commandの識別子・順序・行・種別を照合する。Releaseビルドの警告・エラー0の後、3つのTUnitプロジェクトとCLIの`--help`を実行する。 |

### 環境と既存構成

[環境情報](../../../artifacts/simd/task-1.1-20261018T115544Z/dotnet-info.stdout.log)で.NET SDK10.0.402、.NET runtime10.0.13、Windows10.0.26300・x64を確認した。`global.json`は配置されていない。

`WasmSharp2.slnx`にはランタイム、命令生成器、公式CLI、3つのテストプロジェクトと変換器fixtureが含まれる。ランタイム・CLI・3つのテストプロジェクトは`net10.0`、生成器はコンパイラ用の`netstandard2.0`である。標準検証コマンドは[README](../../../README.md)、[通常CI](../../../.github/workflows/unit-tests.yml)、[ランナーのガイド](../../../tools/WasmSharp.TestSuiteRunner/README.md)の既存手順を使用した。

### 固定素材と比較元

直前の受入済みbaselineは[tables-referencesの8.4](../tables-references/acceptance.md#84-比較済み結果の明示baseline保存)が後続機能用に指定する保存結果である。設計時点から比較元の変更はなかった。

| 項目 | 確認結果 |
| --- | --- |
| 固定spec | `05ca4182176763112561ae20153975c12bd689e4`（v2.0.0） |
| 固定WABT | `c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`（1.0.42とDataCount修正） |
| 受入済みbaseline | [task-8.4-20261018T090714Z/run-baseline.json](../../../artifacts/tables-references/task-8.4-20261018T090714Z/run-baseline.json) |
| baselineのSHA-256 | `9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496` |
| 対応manifest | [wabt-commit-20261016/corpus/manifest.json](../../../artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json) |
| manifestのSHA-256 | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| profile | [core2-profile.json](../../../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)とmanifestのprofile全体が一致 |
| 公式入力 | 全147入力、うちSIMD57入力 |
| 変換済み素材 | 全5,821件（JSON147、wasm4,597、wat1,077） |
| 公式command | 全53,907件 |

[素材監査](../../../artifacts/simd/task-1.1-20261018T115544Z/prerequisites-audit.json)で、固定spec・WABTのHEADと変更なし、147入力のGit blobおよび作業コピーのSHA-256、5,821素材の存在とSHA-256を照合した。baseline内のcorpus全体とmanifest、参照するmanifestのpathも一致した。変換済みJSONとbaselineの全commandは入力相対path・command添字・順序・行・種別が一致し、重複はなかった。manifestとbaselineの処理・出力は完了していた。

profileの全21機能は7機能がON、14機能がOFFである。`simd`はON、`relaxed-simd`はOFFのままであり、各入力の変換引数に`--enable-all`と`--no-check`が含まれないことを確認した。profileと素材の再生成は行っていない。

保存済みbaselineを再集計した分類は次のとおりで、tables-referencesの最新受入記録と一致した。

| 分類 | 件数 |
| --- | ---: |
| passed | 27,482 |
| runtime_unsupported | 1,079 |
| blocked | 24,269 |
| out_of_scope | 1,077 |
| failed | 0 |
| runner_error | 0 |
| 合計 | 53,907 |

残る未対応と前提不成立は、[tables-referencesの8.3](../tables-references/acceptance.md#83-対象ケースと残る未対応の起因の照合)でSIMD由来と記録されている。今回の確認は比較元の同定と素材照合であり、SIMDの機能受入やCore 2.0全体の完成を意味しない。

### 既存fixtureの能力

[HostLinkingModuleBinary](../../../tests/WasmSharp.Tests/Fixtures/HostLinkingModuleBinary.cs)は、必要な入力を既に構成できる。

| 入力 | 既存の構成方法 |
| --- | --- |
| SIMD命令と即値 | `Code`は命令byte列をそのまま本体へ追加する。0xFDと命令番号、16byte定数、shuffle、lane、memargとlaneを任意のbyte列で指定できる。`Unsigned`でu32のLEB128を作り、非最短・切断符号化はrawのbyte列で保持できる。 |
| v128関数型・locals | `Types`・`TypeVector`と`Code`のlocalsは任意の型byteを受け取り、v128の`0x7B`も保持する。 |
| v128 global | `Globals`は型byte・mutable指定・初期化式byte列を受け取るため、v128型と`v128.const`初期化式をそのまま配置できる。 |
| memoryとdata | `Memories`・`Imports`・`Limits`でmemoryを定義・importでき、`Section`でdata等を配置できる。命令内のmemargは`Unsigned`またはrawのbyte列で作れる。 |

既存の[TypeVectorテスト](../../../tests/WasmSharp.Tests/Fixtures/HostLinkingModuleBinary_TypeVectorTests.cs)はv128を含む全値型の保持を、[Createテスト](../../../tests/WasmSharp.Tests/Fixtures/HostLinkingModuleBinary_CreateTests.cs)は複合即値・非最短符号化・切断入力・ホスト連携sectionの構成を確認している。これらは今回のランタイム全件実行に含まれる。

不足する補助はなく、fixture・製品・テストコードの追加や変更は行っていない。動作追加のない前提確認と文書作成のため、RED用の失敗テストとfeature flagは不要とした。SIMDのDecode・Validate・実行成功は後続タスクで接続した時点で検証する。

### 実行結果

以下を順に実行した。各コマンドの引数・作業ディレクトリ・開始/終了UTC・終了値・標準出力/標準エラーの行数は[commands.json](../../../artifacts/simd/task-1.1-20261018T115544Z/commands.json)、TRXの集計は[validation-summary.json](../../../artifacts/simd/task-1.1-20261018T115544Z/validation-summary.json)に保存した。

```powershell
dotnet --info
python artifacts/simd/task-1.1-20261018T115544Z/audit-prerequisites.py
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/simd/task-1.1-20261018T115544Z/runtime
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/simd/task-1.1-20261018T115544Z/generators
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/simd/task-1.1-20261018T115544Z/runner
tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe --help
```

| 確認 | 結果 | 記録 |
| --- | --- | --- |
| 素材と比較元の監査 | 終了0 | [audit.result.json](../../../artifacts/simd/task-1.1-20261018T115544Z/audit.result.json)・[監査スクリプト](../../../artifacts/simd/task-1.1-20261018T115544Z/audit-prerequisites.py) |
| Releaseビルド | 終了0、警告0、エラー0 | [build.stdout.log](../../../artifacts/simd/task-1.1-20261018T115544Z/build.stdout.log) |
| ランタイム | 終了0、3,761件成功、失敗・スキップ0 | [実行記録](../../../artifacts/simd/task-1.1-20261018T115544Z/runtime.result.json)・[TRX](../../../artifacts/simd/task-1.1-20261018T115544Z/runtime/WasmSharp.Tests_net10.0_x64.trx) |
| 生成器 | 終了0、106件成功、失敗・スキップ0 | [実行記録](../../../artifacts/simd/task-1.1-20261018T115544Z/generators.result.json)・[TRX](../../../artifacts/simd/task-1.1-20261018T115544Z/generators/WasmSharp.Generators.Tests_net10.0_x64.trx) |
| 公式ランナー | 終了0、702件成功、失敗・スキップ0 | [実行記録](../../../artifacts/simd/task-1.1-20261018T115544Z/runner.result.json)・[TRX](../../../artifacts/simd/task-1.1-20261018T115544Z/runner/WasmSharp.TestSuiteRunner.Tests_net10.0_x64.trx) |
| CLI起動 | 終了0 | [help.result.json](../../../artifacts/simd/task-1.1-20261018T115544Z/help.result.json) |

検証後もbaselineとmanifestのSHA-256が上記の値であることを確認した。3プロジェクト合計4,569件の既存テスト成立は、今回の前提確認の範囲である。公式全体の`run`・`compare-run`・`verify`・`baseline-save`、WABTのビルドと素材再生成は今回実行していない。

### 後続の実行・比較

公開経路へ接続した機能単位で全体を実行し、上記baselineと同じ公式command単位で比較する。各回は新しい専用出力先を用い、既存出力とbaselineを保持する。以下は後続タスク用の例であり、今回の実行記録には含めない。

```powershell
$runner = './tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe'
$taskOutput = 'artifacts/simd/task-3.2-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
New-Item -ItemType Directory -Path $taskOutput | Out-Null
& $runner run --manifest artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json --output "$taskOutput/run.json"
& $runner compare-run --baseline artifacts/tables-references/task-8.4-20261018T090714Z/run-baseline.json --current "$taskOutput/run.json" --output "$taskOutput/comparison.json"
```

`run`と`compare-run`の終了0だけでは受入としない。以前のpassedの別分類への変化・欠落を回帰として拒否し、要件8.2〜8.7の対象確認・原因記録・最終判定・明示保存は各後続タスクで行う。

### 独立レビューと完了確認

独立レビューは`APPROVED`で、指摘はなかった。[レビューの実行記録](../../../artifacts/simd/review-1.1-resume-20261018T130404Z/commands.json)でReleaseビルドの警告・エラー0、3プロジェクト合計4,569件成功・失敗とスキップ0、CLI起動の終了0を再確認した。`kiro-verify-completion`のタスク1.1判定は`VERIFIED`とし、未確認範囲は後続のSIMD機能検証と公式全体実行である。

## 1.2 数値型効果の共通表現

要件2.1・2.7と設計のInstructionMetadata、ExpressionDecoder・FunctionValidatorに従い、数値型効果を不変の順序付き入力型列と1結果型へ統一した。既存の属性記法は同型列へ変換し、validatorは逆順にpopする。SIMD接続や入力数・型の受入範囲は変更していない。

テストを先に変更し、ビルド成功後に生成器の動的コンパイル7ケースが新契約の不足で失敗するREDを確認した。[実装時のログ](../../../artifacts/simd/task-1.2-20261018T130820Z/)を保存している。独立レビューは`APPROVED`で、[再検証ログ](../../../artifacts/simd/review-1.2-20261018T131402Z/)ではReleaseビルドの警告・エラー0、ランタイム3,761件・生成器106件・ランナー702件成功、失敗とスキップ0、全コマンド終了0、変更6ファイルの整形チェック成功を確認した。`kiro-verify-completion`のタスク1.2判定は`VERIFIED`で、未解決の指摘はない。公式全体runは機能接続後に行う。

## 1.3 メモリ存在とalignmentの共通検証

要件1.6・2.4・2.5に対応する準備として、`FunctionValidator`の既存検査を`ValidateMemoryAccess`へ抽出した。memory存在、アクセス情報、自然alignment、operandの検査順と診断位置を維持した。既存テストが正常・不正・到達不能を確認するためテストの追加は行っていない。

[実装時のログ](../../../artifacts/simd/task-1.3-20261018T131740Z/)と[独立レビューのログ](../../../artifacts/simd/review-1.3-20261018T132116Z/)で、Releaseビルドの警告・エラー0、ランタイム3,761件成功、失敗とスキップ0、終了0、整形と差分チェック成功を確認した。独立レビューは`APPROVED`、`kiro-verify-completion`のタスク1.3判定は`VERIFIED`で、未解決の指摘はない。

## 1.4 共有する浮動小数点の純粋処理

要件6.2・6.3・6.4に対応する先行整理として、4つのscalar min/maxから純粋処理を同じpartial内のprivate補助へ抽出した。NaN・符号付きゼロの規則とoperand順を維持し、既存の算術結果・丸め・飽和i32変換の補助は移動せず再利用する。動作を変えない抽出で、必要な境界条件を既存テストが検証するため追加テストは作成していない。

[実装時のログ](../../../artifacts/simd/task-1.4-20261018T132438Z/)と[独立レビュー](../../../artifacts/simd/review-1.4-20261018T132831Z/review-verdict.md)で、Releaseビルドの警告・エラー0、ランタイム3,761件成功、失敗とスキップ0、全終了0、整形と差分チェック成功を確認した。独立レビューは`APPROVED`、`kiro-verify-completion`のタスク1.4判定は`VERIFIED`で、未解決の指摘はない。

## 2.1 SIMDの生成契約

要件1.2・1.3・2.1〜2.4の準備として、1〜3入力の型列とv128結果、SIMD即値、`LaneCount`、vectorアクセス幅とlaneメモリ規則を生成可能にした。入力配列をコピーし、不正な宣言は既存診断で拒否する。全236命令の未対応状態は維持し、Decode・Validate・実行の接続は後続で行う。

[初回実装記録](../../../artifacts/simd/task-2.1-20261018T133210Z/status-report.md)では、一時フラグOFFで150件中29件失敗、ONで150件成功、フラグ除去後も成功を確認した。後から追加した9件は既存実装の補強テストで、個別のREDは取得していない。[初回レビュー](../../../artifacts/simd/review-2.1-20261018T133957Z/review-verdict.md)で重複`LaneCount`による生成器例外を検出したため、[修正記録](../../../artifacts/simd/task-2.1-remediation-1-20261018T134355Z/status-report.md)の回帰テストで修正前のREDを取得し、既存`WSIG002`で拒否する条件を追加した。

[独立再レビュー](../../../artifacts/simd/review-2.1-r2-20261018T134655Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、生成器160件・ランタイム3,761件成功、失敗とスキップ0、全終了0、変更9ファイルの整形と差分チェック成功を確認した。`kiro-verify-completion`のタスク2.1判定は`VERIFIED`で、未解決の指摘はない。

## 2.2 128bitのlane操作とbyte変換

要件3.1・4.1・6.6に対応する内部`SimdValue`を追加した。上下64bitを保持し、8/16/32/64bitのlane取得・切詰め置換と、16byteのlittle-endian相互変換を提供する。`WasmValue`とBCLだけに依存し、操作ごとの配列確保やCPU固有分岐はない。

[実装記録](../../../artifacts/simd/task-2.2-20261018T135140Z/status-report.md)に5操作のREDとGREENを保存した。一時フラグ除去後の[独立レビュー](../../../artifacts/simd/review-2.2-20261018T140227Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、関連37件を含むランタイム3,798件成功、失敗とスキップ0、終了0と整形成功を確認した。64bit境界、64bit幅のmask、非対象lane保持、NaN payload・符号付きゼロを含むbit往復を確認し、`kiro-verify-completion`のタスク2.2判定は`VERIFIED`とした。未解決の指摘はない。公開SIMD経路とCPU支援無効時のhandler検証は後続で行う。

## 2.3 SIMD即値の読み取りと保持の準備

要件1.1〜1.6の接続準備として、4種類のSIMD即値の読み取りと、`DecodedInstruction`の`ShuffleLanes`・`LaneIndex`を追加した。shuffleは入力からコピーして所有し、memargとlaneを別々に保持する。lane・shuffleの範囲検査はValidateへ残し、既存readerと元位置を維持した。

[実装記録](../../../artifacts/simd/task-2.3-20261018T140646Z/status-report.md)と[独立レビュー](../../../artifacts/simd/review-2.3-20261018T141117Z/review-verdict.md)で、Releaseビルドの警告・エラー0、ランタイム3,798件・生成器160件成功、失敗とスキップ0、終了0、整形と差分チェック成功を確認した。未接続の準備として独立レビューは`APPROVED`、`kiro-verify-completion`のタスク2.3判定は`VERIFIED`とした。新経路の正常・切断・LEB128検証は未実施で、3.1・8.1・8.2・8.4の接続後に行う。全236命令はまだ未対応のままである。

## 2.4 SIMD静的制約と線形化の準備

要件1.5・1.6・2.1〜2.5・2.7の準備として、定数型、lane・shuffle制約、laneメモリ型効果と実行表現への即値引渡しを追加した。到達不能でも静的制約を確認し、laneメモリは固定参照実装と同じmemory→alignment→lane→operandの順序を保つ。検証成功時だけコードを返す既存契約を維持し、selectは変更していない。

[実装記録](../../../artifacts/simd/task-2.4-20261018T141555Z/status-report.md)と[独立レビュー](../../../artifacts/simd/review-2.4-20261018T142026Z/review-verdict.md)で、Releaseビルドの警告・エラー0、ランタイム3,798件・生成器160件成功、失敗とスキップ0、各終了0、整形と差分チェック成功を確認した。未接続の準備として独立レビューは`APPROVED`、`kiro-verify-completion`のタスク2.4判定は`VERIFIED`とした。新しい型順・lane・shuffle・alignment・到達不能の否定経路は3.1・8.1・8.2・8.4で接続後に検証する。

## 3.1 定数・global初期化とv128 selectの接続

要件1.1〜1.4・1.6・2.6・2.7・3.1・3.2・3.4・3.5に対応する接続として、`v128.const`を既存`PushConstant`と定数初期化式へ接続し、typed/untyped selectのv128未対応制限を除去した。公開API・decoder・handler・`ConstantInitializer`は既存処理を使い、専用評価器は追加していない。残る235個の0xFD命令は未接続である。

[実装記録](../../../artifacts/simd/task-3.1-20261018T142431Z/status-report.md)で、一時フラグOFFの公開経路11件すべてのRED、ONのGREEN、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-3.1-20261018T143705Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム3,826件・生成器160件・ランナー702件成功、失敗とスキップ0、各終了0、13ファイルの整形と差分チェック成功を確認した。byte列・Stream、入力変更からの独立、NaN payload・符号付きゼロを含む128bit、global初期化と選択、切断・不正LEB128・要求型不一致の段階と位置を確認した。

未対応期待の更新と廃止は次のとおりである。

- `ExpressionDecoder_ReadTests`と`ModuleDecoder_DecodeFailureTests`の`FD0C`各1件、`WasmModule_DecodeTests`の`FD0C`・`FD8C00`とbyte列・Streamの組合せ4件は、constが未対応であることだけを期待するため廃止し、完全即値・切断・LEB不正・型不一致の確認へ置き換えた。
- `ModuleDecoder_ReadElementTests`と`WasmModule_DecodeGlobalsTests`は完全即値、切断、要求型不一致に分け、sectionと位置、Decode・Validateの境界を維持した。
- selectの後段不一致、再検証と部分コード未公開、到達不能のtyped selectを確認した。診断再走査は完全const後の未対応loadで元の境界不正を保持し、const切断の位置は別に確認した。`InspectImports`の非実行と未確認範囲も保持した。

`kiro-verify-completion`のタスク3.1判定は`VERIFIED`で、未解決の指摘はない。公式全体run・比較は3.2、演算を含む定数式拒否は8.1、広い共通経路・ホスト・状態共有は9.xで検証する。
