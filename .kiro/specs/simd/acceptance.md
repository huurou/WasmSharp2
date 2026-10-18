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

## 3.2 定数・選択の統合結果の公式全体確認

要件1.6・8.1〜8.4と設計の「ConformanceVerification」「失敗処理（Error Handling）」「公開経路の統合確認」「公式受入と最終判定」に従い、3.1の接続後に固定147入力・53,907commandを新しい出力先へ全体実行し、直前の受入済みbaselineと同一の入力・command単位で比較した。[Task Brief](../../../artifacts/simd/task-3.2-20261018T144703Z/task-brief.md)の完了条件は、定数・select起因のfailedと既存passedの回帰0、残る未接続命令の未対応・blockedの原因確認、結果保存である。

| 検証 | 結果 | 記録 |
| --- | --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --warnaserror` | 終了0、警告0・エラー0 | [build.log](../../../artifacts/simd/task-3.2-20261018T144703Z/build.log) |
| ビルド済みCLIの`--help` | 終了0 | [help.log](../../../artifacts/simd/task-3.2-20261018T144703Z/help.log) |
| 固定manifestの`run` | 終了0、処理・出力完了、入力異常・未処理0 | [run.json](../../../artifacts/simd/task-3.2-20261018T144703Z/run.json) |
| tables-references受入済みbaselineとの`compare-run` | 終了0、成立・完了、差分659件 | [comparison.json](../../../artifacts/simd/task-3.2-20261018T144703Z/comparison.json) |
| 全ケース・固定素材・依存原因の監査 | 終了0 | [results-audit.json](../../../artifacts/simd/task-3.2-20261018T144703Z/results-audit.json) |

結果はpassed27,918、runtime_unsupported828、blocked24,084、out_of_scope1,077、failed0、runner_error0だった。baselineのpassed27,482件はすべてpassedを保持し、条件差・出典差・素材差・追加・欠落・回帰・未比較はすべて0件である。659件の差分は、runtime_unsupportedからpassedへの251件、blockedからpassedへの185件、runtime_unsupportedのまま`v128.const`を通過して別の未接続命令へ停止位置が進んだ223件である。

全53,907件のID・順序・行・種別・期待診断・期待値を元の公式JSONとbaselineへ照合し、147入力と5,821素材のhashを再計算した。SIMD有効・relaxed-SIMD無効のprofileと実行ポリシーは不変である。manifestはSHA256 `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e`、baselineは`9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496`、profileは`53e65ededab6814a6f73ac17ff286cedb35221b01aea83bbdd20e0ab15b452bc`を保持した。今回のrunのSHA256は`8a7f7d312c4e68e09b79a23a268cc9b177f77fbfb9bc5b15b121be2428506de6`である。

定数を通る具体例として、`simd/simd_const.wast#198`のf32x4定数、`#573`のmoduleと`#574`の分岐戻り値、`#599`のglobal定義と`#602`のglobal.getがpassedとなった。[ケース記録](../../../artifacts/simd/task-3.2-20261018T144703Z/const-select-cases.json)には公式command・期待・実際・段階・診断を保存した。新たにpassedとなったv128戻り値180件は、公式lane値から再構成した128bitと上下64bitの一致も確認した。全体のpassed否定assertion4,697件は期待診断への前方一致を満たす。`select.wast#119`は`type mismatch`、Validate、`WasmValidateException`、byte_offset27・function_index0・section_id10でpassedだった。定数のテキスト形式に関する否定180件は従来どおりout_of_scopeで、バイナリ診断の成功には合算していない。

固定`select.wast`の148件はすべてpassedで、baselineの各ケース結果と完全に一致した。同ファイルはv128を扱わず、固定147入力のうちv128と通常selectが共存する`simd_boolean.wast`の6箇所もi32候補を選択する。typed/untypedのv128 select自体は公式素材に収録されていないため、3.1で実行済みの公開4段階による選択6件、候補・条件型不一致6件、終端の余剰値2件、到達不能typed1件の計15件がすべてpassedであることを[保存済みTUnit結果の抜粋](../../../artifacts/simd/task-3.2-20261018T144703Z/supporting-select-tests-3.1.json)と[3.1の独立レビュー](../../../artifacts/simd/review-3.1-20261018T143705Z/review-verdict.md)へ結び付けた。NaN payload・符号付きゼロを含むbit保持、型不一致の`type mismatch`とValidate位置・section10、再試行時に成功状態を公開しない確認は3.1の結果を再利用し、今回再実行していない。

残るruntime_unsupported828件は、既存宣言から確認した未接続235個の0xFD命令のうち226種類の公開診断featureへ対応した。全件がDecodeの`WasmUnsupportedFeatureException`で元位置と未確認範囲を保持し、`v128.const`やv128 selectによる未対応はない。blocked24,084件は全件の`cause.direct`を再帰し、求めた起因が記録済みの`origins`と一致し、上記未接続命令の未対応にのみ到達した。[原因一覧](../../../artifacts/simd/task-3.2-20261018T144703Z/remaining-causes.json)には各ID・出典行・診断・直接依存・起因featureを保存し、期待・実際は同じrunの各IDへ対応付けた。ファイル名だけで分類していない。`simd_const.wast`全体もpassed554、out_of_scope180、`i32x4.add`によるruntime_unsupported1件（`#605`）、それに依存するblocked22件であり、この残りを定数の成功へ読み替えていない。

[事前確認](../../../artifacts/simd/task-3.2-20261018T144703Z/preflight.json)と[実行後確認](../../../artifacts/simd/task-3.2-20261018T144703Z/postflight.json)でsrc・tests・tools配下668ファイル、CLI・ランタイムのバイナリ、固定素材・profile・baselineの不変を確認した。ビルド後のCLI・ランタイムのassembly情報は対象HEAD`0f7e688adcddce3860a1d554438388e98a8e0135`を含む。[コマンド・終了値](../../../artifacts/simd/task-3.2-20261018T144703Z/commands.json)と各ログ、[監査スクリプト](../../../artifacts/simd/task-3.2-20261018T144703Z/audit-results.py)を保存した。初回の記録用スクリプトは成功済みビルドの日本語「0 エラー」の集計で終了1となり、読み取りの正規表現だけを修正して続行した。製品の失敗ではなく、詳細は[記録](../../../artifacts/simd/task-3.2-20261018T144703Z/validation-script-note.json)に残した。

製品・テストは変更しておらず、3.1で成功した3テストプロジェクトは重複実行していない。本節は定数・選択を接続した単位の確認であり、未接続235命令を残す。`verify`・`baseline-save`は実行しておらず、SIMD全体の機能受入およびCore 2.0全体の完成は未判定である。

[独立レビュー](../../../artifacts/simd/review-3.2-20261018T145754Z/verdict.md)は`APPROVED`で、別の全体run・比較と原因監査でも同じ件数、既存passedの維持、回帰・欠落・未比較0を確認した。親は保存されたビルド・比較・監査の出力と終了値、変更差分を確認し、`kiro-verify-completion`のタスク3.2判定を`VERIFIED`とした。未解決の指摘はない。

## 4.1 splat・extract・replaceの実行

要件4.1に対応する20handlerを`Interpreter.SimdLane.cs`へ追加した。既存`SimdValue`を使い、整数の切詰め・符号拡張・ゼロ拡張、floatのbit運搬と非対象laneの保持を扱う。設計で指定された最小の`SimdHandlerFixture`でlane即値を渡し、各メソッドのテスト60件で先頭・末尾lane、NaN payload・±0、結果型・sentinel・stack数を確認した。命令宣言への接続は8.1で行う。

[実装記録](../../../artifacts/simd/task-4.1-20261018T150640Z/StatusReport.md)では、一時フラグOFFの60件失敗、ONの60件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-4.1-20261018T151358Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム3,886件成功、失敗・スキップ0、終了0、変更22C#の整形・空白検査成功を確認した。親はhandler・境界テストと実際の出力・終了値を確認し、`kiro-verify-completion`のタスク4.1判定を`VERIFIED`とした。未解決の指摘はない。公開経路と公式全体実行は未実施である。

## 4.2 shuffle・swizzleと128bit論理演算

要件4.2〜4.4に対応する8handlerを追加した。shuffleの2operand連結順、swizzleの範囲外ゼロと6種類の128bit論理演算を扱う。21ケースでshuffleの0/15/16/31、swizzleの15/16/255、上下64bit、非可換演算のoperand順、bit単位mask、結果型・sentinel・stack数を確認した。既存20handlerとhelperは変更していない。

[実装記録](../../../artifacts/simd/task-4.2-20261018T151906Z/status-report.md)で一時フラグOFFの21件失敗、ONの21件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-4.2-20261018T152555Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム3,907件成功、失敗・スキップ0、終了0、変更10C#の整形・空白検査成功を確認した。親は最終差分・境界テストと出力・終了値を直接確認し、`kiro-verify-completion`のタスク4.2判定を`VERIFIED`とした。未解決の指摘はない。公開経路への接続と公式全体実行は8.1で行う。

## 4.3 整数比較と縮約判定

要件4.5・4.6に対応する整数比較36handlerと縮約9handlerを追加した。比較のlane走査を共有し、符号解釈を型別predicateへ置いた。`any_true`・`all_true`・`bitmask`はそれぞれ128bit全体の非ゼロ、全laneの非ゼロ、lane最上位bitの集合を扱う。既存28handlerとhelperは維持した。

[実装記録](../../../artifacts/simd/task-4.3-20261018T153209Z/)で一時フラグOFFの111件失敗、ONの111件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-4.3-20261018T154026Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,018件成功、失敗・スキップ0、終了0、変更46C#の整形・空白・hash確認成功を確認した。固定参照の45命令集合と一致し、比較58ケース・縮約53ケースの期待定数も独立計算で一致した。親は比較・縮約処理、代表的な境界テスト、出力・終了値を直接確認し、`kiro-verify-completion`のタスク4.3判定を`VERIFIED`とした。未解決の指摘はない。公開経路と公式全体実行は8.1で行う。

## 4.4 浮動小数点のvector比較

要件4.5に対応するf32x4・f64x2の各6比較を追加した。型別static predicateでbitsをfloat・doubleへ解釈し、既存`CompareSimdLanes`でlaneごとの全bit1/0を構築する。54ケースでNaNの左右配置、±0の両順、無限大・有限値の大小と等値、上下64bit、結果型・sentinel・stack数を確認した。既存73handlerと共通処理は維持した。

[実装記録](../../../artifacts/simd/task-4.4-20261018T154828Z/status-report.md)で一時フラグOFFの54件失敗、ONの54件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-4.4-20261018T155543Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,072件成功、失敗・スキップ0、終了0、変更13C#の整形・空白・hash一致を確認した。固定参照の12命令集合と54ケースの期待maskの独立計算も一致した。親は最終差分・代表的境界テストと実際の出力・終了値を確認し、`kiro-verify-completion`のタスク4.4判定を`VERIFIED`とした。未解決の指摘はない。公開接続と公式全体実行は8.1で行う。

## 5.1 通常の整数算術とシフト

要件5.1・5.3に対応する31handlerを`Interpreter.SimdInteger.cs`へ追加した。stack操作と純粋なlane計算を分け、各幅のabs・neg・加減乗算の折返しと、i32シフト量の幅による剰余化を扱う。105ケースで最小負数・overflow、0・幅以上・負数のシフト量、signed/unsigned右シフト、上下64bitとstack契約を確認した。

[実装記録](../../../artifacts/simd/task-5.1-20261018T160155Z/StatusReport.md)の初回REDは104件失敗・1件成功だった。非負入力だけのabsは無演算と同値だったため、ON側の編集後に入力を正数・負数の混在へ直し、実装を保持したままOFFで105件すべての失敗を確認し直した。[修正経緯](../../../artifacts/simd/task-5.1-20261018T160155Z/RedRevision.md)と初回ログを保持している。続くONは105件成功し、一時フラグ除去後も成功した。

[独立レビュー](../../../artifacts/simd/review-5.1-20261018T161121Z/review-verdict.md)は`APPROVED`で、編集順序の経緯をFYIとして記録した。Releaseビルドの警告・エラー0、ランタイム4,177件成功、失敗・スキップ0、終了0、変更32C#の整形・空白・hash確認成功を確認した。固定31命令の集合と、多倍長整数・byte切出しモデルによる105期待定数の独立計算も一致した。親は算術・シフト処理と代表的境界テスト、実際の出力・終了値を確認し、`kiro-verify-completion`のタスク5.1判定を`VERIFIED`とした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 5.2 飽和算術とnarrow

要件5.2に対応する飽和加減算8handlerとnarrow4handlerを追加した。同幅の二項演算処理を再利用し、narrowはsigned入力の飽和と第1・第2operandの連結を専用のstack処理・純粋なlane処理で扱う。12ケースの各vectorに上下限の前後、unsigned減算の負化、unsigned narrowの負入力、source最小・最大、連結順の異なるlaneを含めた。既存31handlerと6helperの本体は維持した。

[実装記録](../../../artifacts/simd/task-5.2-20261018T161708Z/)で一時フラグOFFの12件失敗、ONの12件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-5.2-20261018T162636Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,189件成功、失敗・スキップ0、終了0、変更13C#の整形・空白確認成功を確認した。固定参照の12命令集合と、12期待vectorの独立した整数計算も一致した。親は飽和・narrow処理と代表的境界テスト、実際の出力・終了値を確認し、`kiro-verify-completion`のタスク5.2判定を`VERIFIED`とした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 5.3 整数min/max・切上げ平均・popcnt

要件5.4に対応する15handlerを追加し、既存の単項・二項演算処理を再利用した。signed/unsigned min/maxは選択したlaneのbit列を返し、avgr_uは広い中間値で奇数和を切り上げ、popcntは各byteのbit数を数える。19ケースで符号境界、大小・等値、上下64bit、平均の偶数和・奇数和・最大値同士、全16byteのbit数とstack契約を確認した。既存43handlerと9helperの本体は維持した。

[実装記録](../../../artifacts/simd/task-5.3-20261018T163225Z/StatusReport.md)で一時フラグOFFの19件失敗、ONの19件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-5.3-20261018T163936Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,208件成功、失敗・スキップ0、終了0、変更16C#の整形・空白・hash確認成功を確認した。固定参照の15命令集合と19期待vectorの独立計算も一致した。親は最終predicate、代表的境界テスト、実際の出力・終了値を直接確認し、`kiro-verify-completion`のタスク5.3判定を`VERIFIED`とした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 5.4 low/highの拡張と拡張乗算

要件5.5に対応するextend12handlerとextmul12handlerを追加した。context操作と純粋なlane処理を分け、選択した入力の半分を拡張先のlane 0から配置する。32→64bitのsigned積はlong、unsigned積はulongで保持する。32ケースで上下の非対称入力、符号境界、拡張幅の積、最大uint同士の積、結果型・sentinel・stack数を確認した。既存58handlerと9helperの本体は維持した。

[実装記録](../../../artifacts/simd/task-5.4-20261018T164644Z/)で一時フラグOFFの32件失敗、ONの32件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/review-5.4-20261018T165447Z/review-verdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,240件成功、失敗・スキップ0、終了0、変更25C#の整形・空白確認成功を確認した。固定参照の24命令集合・subopcodeと32期待vectorの独立計算が一致し、各ケースが逆の半分・符号解釈も検出した。24テストのhashはRED・最終・レビュー時点で一致した。親は拡張処理・代表的境界テスト、実際の出力・終了値を確認し、`kiro-verify-completion`のタスク5.4判定を`VERIFIED`とした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 5.5 隣接laneの拡張加算とdot

要件5.6のpairwise4handlerとdot1handlerを追加し、既存の単項・二項処理を再利用した。pairwiseは出力幅ごとの隣接2laneを符号解釈に従って加算し、dotはlongの積和をi32へ折り返す。5ケースに符号境界、上下で異なるlane、dotの2147483648から0x80000000への折返しを含め、結果型・128bit・sentinel・stack数を確認した。既存82handlerと13helperの本体は維持した。

[実装記録](../../../artifacts/simd/task-5.5-20261018T170156Z/StatusReport.md)で、一時フラグOFFの5件失敗・終了2、ONの5件成功、フラグ除去後の成功を確認した。RED終了値の報告文の誤記は保存JSONと同じ2へ訂正し、元のログは保持した。[独立レビュー](../../../artifacts/simd/task-5.5-independent-review-20261018T171130Z/ReviewVerdict.md)は`APPROVED`で、Releaseビルドの警告・エラー0、ランタイム4,245件成功、失敗・スキップ0、終了0、6C#の整形・空白確認成功を確認した。固定5命令集合と5期待vectorの独立計算が一致した。保存済み公式JSONの58期待値も固定規則の計算と一致したが、公式ランタイム実行とは区別する。親は5wrapper・dotテスト、実際の出力・終了値を確認し、kiro-verify-completionのタスク5.5判定をVERIFIEDとした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 5.6 Q15の丸め付き飽和乗算

要件5.6のQ15 handlerを追加した。既存の二項処理と飽和処理を再利用し、signed i16の積に16384を加え、15bit算術シフト後にi16へ制限する。中間値はintで保持できる。2vector・16laneで最小値同士の32767、最大値同士の32766、正負の丸め境界と型・128bit・stack契約を確認した。既存87handlerと13helperの本体は維持した。

[実装記録](../../../artifacts/simd/task-5.6-20261018T171859Z/StatusReport.md)の一時フラグOFFは2件失敗・終了2、ONは同じ2件成功・終了0。[独立レビュー](../../../artifacts/simd/review-5.6-20261018T172509Z/ReviewVerdict.md)はAPPROVEDで、フラグ除去後のReleaseビルド警告・エラー0、ランタイム4,247件成功・失敗/スキップ0・終了0、明示2C#の整形・空白確認成功、16laneの独立計算一致を確認した。REDと最終のテストは同一である。親は計算式・境界テスト・実際の出力と終了値を確認し、kiro-verify-completionのタスク5.6判定をVERIFIEDとした。未解決の指摘はない。公開接続と公式全体実行は8.2で行う。

## 6.1 浮動小数点の符号bit操作と基本算術

要件6.1・6.2の基本算術・6.6に対応し、f32x4/f64x2のabs・neg・add・sub・mul・div・sqrtの14handlerを追加した。stack処理と純粋なlane計算を分け、abs/negはrawbit、算術は既存の同精度NaN処理を使う。60ケースでsignaling NaNを含むpayload保持、±0、無限大、非正規化数、operand順、丸めと平方根の境界、128bit・stack契約を確認した。既存製品コードは変更していない。

[実装記録](../../../artifacts/simd/task-6.1-20261018T172944Z/StatusReport.md)でOFF60全件失敗・終了2、ON60成功を確認した。対象コードの浮動小数点計算を使わない有理数・高精度平方根による160laneの期待bit計算も一致した。[独立レビュー](../../../artifacts/simd/review-task-6.1-20261018T174020Z/review.md)はAPPROVEDで、フラグ除去後のReleaseビルド警告・エラー0、ランタイム4,307件成功・失敗/スキップ0・終了0、15C#の整形・空白・hash検査、重要な18laneの独立計算が成功した。実装側では新規プロセスのDOTNET_EnableHWIntrinsic=0による60件も成功し、設定はその実行だけに限定した。親はwrapper・4helperと代表的テスト、実際の出力・終了値を確認し、kiro-verify-completionのタスク6.1判定をVERIFIEDとした。未解決の指摘はない。公開接続・公式全体実行は8.3、統合状態の支援無効確認は10.1で行う。

## 6.2 丸めと2種類の最小・最大演算

要件6.2の丸め・6.3に対応する16handlerを追加した。ceil/floor/trunc/nearestは既存の同精度補助でゼロ符号とNaNを扱い、nearestは最近接偶数とする。min/maxは既存補助を再利用し、pmin/pmaxは固定比較で選ばれたoperandのrawbitsを返す。108ケースで正負の丸め境界、±0、非正規化数、無限大、左右のNaN・signaling payload、lane・stack契約を確認した。既存14handler・4helperは維持した。

[実装記録](../../../artifacts/simd/task-6.2-20261018T174633Z/StatusReport.md)でOFF108件失敗・終了2、ON108件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/task-6.2-review-20261018T175337Z/verdict.md)はAPPROVED。Releaseビルド警告・エラー0、ランタイム4,415件成功・失敗/スキップ0・終了0、明示17C#の整形・空白検査成功、108組・288laneの期待定数の独立照合一致を確認した。親は最終wrapper・代表的テスト、実際の出力・終了値を確認し、kiro-verify-completionのタスク6.2判定をVERIFIEDとした。未解決の指摘はない。公開接続・公式全体実行は8.3で行う。

## 6.3 浮動小数点から整数への飽和変換

要件6.4・6.5のzero形式に対応する4handlerを追加した。各handlerは既存TruncateSaturatingI32を再利用し、stack操作と純粋なlane変換を分けた。37ケースでsigned/unsigned境界の内外、正負の小数・ゼロ・非正規化数、NaN・無限大、zero形式の上位lane、128bitとstack契約を確認した。既存製品コードは変更していない。

[実装記録](../../../artifacts/simd/task-6.3-20261018T180049Z/StatusReport.md)の初回OFFは37件失敗・終了2、ONは37件成功だった。最初の全件確認後、自己確認でf32unsignedの末尾laneの期待値が全ケース0だったため、1ケースを-12.5から+12.5へ変更し、出力12を確認するよう補強した。元ログを保持し、再度OFFで37件すべて失敗、ONで37件成功を確認した。補強後の最終結果はlane3-final-*である。

[独立レビュー](../../../artifacts/simd/task-6.3-review-20261018T181114Z/ReviewVerdict.md)はAPPROVED。フラグ除去後のReleaseビルド警告・エラー0、ランタイム4,452件成功・失敗/スキップ0・終了0、5C#の整形・空白・hash検査、37期待vectorの独立照合一致を確認した。補強後REDの全37引数も現在のテストと一致した。親は4wrapper・2helper・代表的テスト、実際の出力と終了値を確認し、kiro-verify-completionのタスク6.3判定をVERIFIEDとした。未解決の指摘はない。公開接続・公式全体実行は8.3で行う。

## 6.4 整数から浮動小数点への変換と精度変更

要件6.5・6.6に対応するconvert4handler、demote・promote各1handlerを追加した。挙動追加に先立ち、既存変換helperを入力幅・出力幅・rawbit演算へ整理し、警告・エラー0のReleaseビルドと既存4クラス37件の成功を確認した。入力と出力のlane数の小さい方だけを変換し、結果の未使用laneはゼロを保つ。

[実装記録](../../../artifacts/simd/task-6.4-20261018T181628Z/StatusReport.md)の新規42ケースでは、signed/unsigned、2^24付近とuint上限、demoteの最近接偶数・中点前後・overflow・underflow、promoteのlow選択、±0・無限大・NaNと128bit・stack契約を確認した。OFF42件失敗・終了2、ON42件成功、フラグ除去後の成功を記録した。ON実装後の4convertテスト名の訂正は入力の符号解釈を明確にする表現変更だけで、REDの入力・期待値とケース数は維持した。

[独立レビュー](../../../artifacts/simd/review-6.4-20261018T182524Z/review.md)はAPPROVED。Releaseビルド警告・エラー0、ランタイム4,494件成功・失敗/スキップ0・終了0、7C#の整形・空白・hash検査、42期待vectorの独立計算一致、先行整理とRED段階の再構成・時刻順を確認した。親は最終wrapper・共通Map・代表的テスト、実際の出力と終了値を確認し、kiro-verify-completionのタスク6.4判定をVERIFIEDとした。未解決の指摘はない。公開接続・公式全体実行は8.3、統合後のHW支援無効確認は10.1で行う。

## 7.1 通常のvector load/store

要件7.1・7.4・7.5に対応するV128Load/V128Storeを追加した。unsigned i32とoffsetをulongで合成し、既存WasmMemoryのTryRead/TryWriteとSimdValueのlittle-endian変換を使って16byteを転送する。22ケースで非整列・末尾ぴったり・1byte超過・ページ横断・0page・最大uintとoffset、全128bitとstack、元の関数index・byte位置を持つtrap、失敗storeの全memory不変を確認した。scalar・fixture・他handlerは変更していない。

[実装記録](../../../artifacts/simd/task-7.1-20261018T183042Z/StatusReport.md)でOFF22件失敗・終了2、ON22件成功、フラグ除去後の成功を確認した。[独立レビュー](../../../artifacts/simd/task-7.1-review-20261018T183701Z/ReviewVerdict.md)はAPPROVED。Releaseビルド警告・エラー0、ランタイム4,516件成功・失敗/スキップ0・終了0、3C#の整形・空白・hash確認が成功した。親は2handler・境界テストと実際の出力・終了値を確認し、kiro-verify-completionのタスク7.1判定をVERIFIEDとした。未解決の指摘はない。公開接続・公式全体実行は8.4で行う。

## 7.2 拡張load・splat load・zero load

要件7.2・7.4・7.5に対応し、拡張6入口と幅を共有するsplat・zero各1入口で12命令を扱う。先に通常loadの読み出し・trap伝播をReadSimdMemoryへ整理し、警告・エラー0のビルドと既存22件成功を確認した。アクセス幅は拡張8byte、splat1/2/4/8byte、zero4/8byteとし、stackallocの未読部分をClearしてから指定幅だけ読む。拡張・複製のlane計算は純粋helperへ分けた。既存storeは維持した。

[実装記録](../../../artifacts/simd/task-7.2-20261018T184236Z/status-report.md)で、テスト名の構文エラーを直した後の警告・エラー0のビルド、新規61件のOFF全失敗・終了2、ON61件成功を確認した。一時フラグ宣言の残存除去と、親の設計照合による純粋helper抽出後に再検証した。最終結果はpure-helper-*である。

[独立レビュー](../../../artifacts/simd/review-task-7.2-20261018T185234Z/review-verdict.md)はAPPROVED。Releaseビルド警告・エラー0、ランタイム4,577件成功・失敗/スキップ0・終了0、9C#の整形・空白・hash・フラグ確認、37成功期待vectorの独立計算一致を確認した。61ケースで全12命令の符号・幅・LE、必要幅の末尾と1byte超過、非整列・ページ横断、uint加算、trap位置とstackを確認した。親は最終コード・代表的テスト、実際の出力と終了値を確認し、kiro-verify-completionのタスク7.2判定をVERIFIEDとした。未解決の指摘はない。laneは7.3、公開接続・公式全体実行は8.4で行う。

## 7.3 lane load/store

要件7.3・7.4・7.5と7.7のhandler範囲に対応し、ByteWidthとLaneIndexを共有するload・store各1入口で8命令を扱う。既存storeの書き込み・trapをWriteSimdMemoryへ先に整理し、警告・エラー0のビルドと既存83件成功を確認した。loadは指定幅を読み対象laneだけ置換し、storeはlittle-endian表現の対象laneだけを既存memoryへ渡す。

[実装記録](../../../artifacts/simd/task-7.3-20261018T185820Z/handoff.md)の54ケースで全4幅の先頭・末尾lane、非整列・メモリ末尾・ページ横断、1byte超過・33bit加算、全128bitまたは全memory、stack・sentinel・trap位置を確認した。OFF54件すべて失敗・終了2、ON54件成功、フラグ除去後の成功を記録した。期待値は製品のlane処理を使わずbyte列から構成した。

[独立レビュー](../../../artifacts/simd/review-7.3-20261018T190447Z/review-verdict.md)はAPPROVED。Releaseビルド警告・エラー0、ランタイム4,631件成功・失敗/スキップ0・終了0、3C#の整形・空白・hash・フラグ確認が成功した。親は最終コード・両テスト、実際の出力と終了値を直接確認し、kiro-verify-completionのタスク7.3判定をVERIFIEDとした。未解決の指摘はない。公開接続と公式全体実行は8.4、Invoke/startの失敗後の統合契約は9.2で確認する。

## 8.1 lane・論理・比較の公開接続

既存の85handlerを命令宣言へ接続し、型列・結果型・即値・lane数と固定付録を照合した。FDは86命令が対応済み、150命令が未接続となった。製品変更はInstructionSetだけ。新規40件は代表14命令の公開Invoke、byte[]/Stream、即値切断、lane上限、shuffle31/32、混合入力の逆順、到達不能、定数式拒否と失敗後の状態を確認する。既存85handlerの246件も成功した。9対象テストの扱いは[引継ぎ](../../../artifacts/simd/task-8.1-20261018T191116Z/handoff.md)に記録し、shuffleの未対応のみの1引数を廃止した。診断再走査2位置を即値先頭へ更新し、非実行・import未確認範囲などの独立目的を保持した。

初回レビューは、保存フラグが85宣言を切り替えていないためREJECTEDとなった。通常の実装前後の失敗・成功を、指定のOFF→ONと説明した点を訂正した。[修正記録](../../../artifacts/simd/task-8.1-20261018T191116Z/remediation-1-20261018T193220Z/handoff-correction.md)では同一ソースの85宣言を既定OFFからDefineConstants指定でONへ切り替え、生成metadata0/85→85/85、同じ40件の全失敗・終了2→全成功・終了0を確認した。完全除去後の8ファイルは初回最終版と全byte・hashが一致し、旧記録は保持した。既存改行も維持した。

[修正後の独立レビュー](../../../artifacts/simd/review-8.1-remediation-independent-20261018T193744Z/review-verdict.md)はAPPROVED。警告・エラー0のReleaseビルド後、4671/160/702件が全成功、失敗・スキップ0・各終了0。整形・差分・hash・フラグ確認も成功した。公式run時との902ソース・5実行artifact・固定素材・結果JSONの同一性を確認し、同じrun/比較を採用した。

[公式結果](../../../artifacts/simd/task-8.1-20261018T191116Z/results-audit.json)は147入力・53907commandでpassed35520、runtime_unsupported526、blocked16784、out_of_scope1077。元27482件と前回27918件のpassedを全保持し、7602件が新たにpassedとなった。期待値・出典・5821素材は不変、否定診断4978件は前方一致、失敗・回帰・欠落・未比較・未処理0。残未対応/blockedは整数260/2453、浮動小数点・変換107/13687、メモリ159/644へ原因を再帰して対応付け、独立監査も一致した。親は原ログ・終了値・最終コードを直接確認し、kiro-verify-completionをVERIFIEDとした。未解決指摘はなく、verifyとbaseline-saveは後続の最終受入で行う。

## 8.2 整数演算の公開接続

既存88handlerを通常・飽和・shift・拡張・積和の宣言へ接続した。固定付録からopcode・名前・型列・結果型・handlerを照合し、shift12宣言の入力は下から[V128,I32]とした。製品変更はこの88宣言だけで、それ以外のInstructionSetは全byte不変。FDは174命令が対応済み、未接続は浮動小数点・変換40とメモリ22。既存88handlerの175テストを再利用した。

[実装記録](../../../artifacts/simd/task-8.2-20261018T194827Z/handoff.md)の新規19件は、公開4段階の境界値、128以上subopcodeの最短・許容非最短LEB128、全4幅shiftの型順と逆順Validate拒否、metadataを確認する。88宣言全体の同一ソースでOFF19件全失敗・終了2→明示DefineConstants指定のON19件全成功・終了0、対象外148metadata不変を確認した。フラグ完全除去後のReleaseビルドは警告・エラー0、4689/160/702件が全成功・失敗/スキップ0・各終了0。初期のassertion規約エラーと補助ログの日本語表記の誤認は解消し、旧ログも保持した。FDDF01の未対応だけを期待する1引数は接続により廃止し、公開境界値テストへ移行した。汎用の位置・未確認範囲の目的は残るmemory2引数で維持した。

[独立レビュー](../../../artifacts/simd/review-8.2-20261018T195855Z/review-verdict.md)はAPPROVED。機械検査・担当境界・最終5hash・既存改行・RED切替を直接確認し、[公式監査](../../../artifacts/simd/task-8.2-20261018T194827Z/results-audit-classified.json)も独立に照合した。147入力・53907commandはpassed38169、runtime_unsupported268、blocked14393、out_of_scope1077。元passed27482と直前35520を全保持し2649件が新たに成功した。全期待値・出典・147source/5821素材・固定条件は不変、否定診断5205件も前方一致、失敗・回帰・欠落・未比較・未処理0。残未対応/blockedは浮動小数点・変換109/13749とメモリ159/644だけで、cause.direct再帰とoriginsの独立照合も一致した。親は原ログ・終了値・最終コードを直接確認し、kiro-verify-completionをVERIFIEDとした。未解決指摘はなく、verify/baseline-saveは最終受入で行う。

## 8.3 浮動小数点・変換の公開接続

既存float30・変換10handlerを型効果と宣言へ接続し、固定付録の40命令とopcode・入力数・結果・即値・handlerを照合した。製品変更は40属性だけで、それ以外のInstructionSetは全byte不変。FDは214命令が対応済みで、残22命令はメモリだけ。既存40handlerの247テストを再利用し、新規公開20ケースとmetadata1ケースでrawbits・NaN payload・±0・pmin/pmaxのoperand順・最近接偶数・飽和・整数変換・demote/promote・未使用laneを確認した。旧未対応ケースの廃止は0件。

[実装記録](../../../artifacts/simd/task-8.3-20261018T200613Z/evidence-index.md)で、40宣言全体のOFF21件全失敗・終了2→同一ソースと期待値のまま明示DefineConstants指定のON21件全成功・終了0を記録した。40フラグ完全除去後のReleaseビルドは警告・エラー0、4710/160/702件全成功・失敗/スキップ0・各終了0。旧174metadata、source/test ID・引数と最終hashを確認し、整形・差分検査も成功した。

[独立レビュー](../../../artifacts/simd/review-8.3-20261018T201449Z/review-verdict.md)はAPPROVED。40枝の展開、全905source・5実行artifactと公式runの同一性、公開20期待vectorの独立計算も一致した。[公式監査](../../../artifacts/simd/task-8.3-20261018T200613Z/results-audit-classified.json)は147入力・53907commandでpassed52027、runtime_unsupported159、blocked644、out_of_scope1077。元27482件と直前38169件のpassedを全保持し13858件が新たに成功した。NaN pattern1592件と具体lane12141件は未変更のValueMatcher/ScriptExecutorで判定し、期待値の正規化や新しい判定器は追加していない。全期待値・出典・147source/5821素材・固定条件は不変、否定診断5295件も前方一致、失敗・回帰・欠落・未比較・未処理0。残159/644はすべてメモリ22命令に原因を再帰して対応付け、独立照合も一致した。親は原ログ・終了値・コードを直接確認し、kiro-verify-completionをVERIFIEDとした。未解決指摘はない。CPU支援無効とverify/baseline-saveは後続の最終受入で確認する。


## 8.4 メモリ命令の公開接続

通常・拡張・splat・zero・laneの22宣言を既存12handlerへ接続し、FD全236命令が対応済みとなった。製品変更は22属性のみ。既存137handlerテストを再利用し、新規75件でbyte/Stream即値切断、全22alignment境界、全8lane上限、memory→alignment→lane→operand順、到達不能、型順、元位置と再検証、公開Invoke/startのtrap・失敗store未変更・先行更新・保存参照・次の呼び出しを確認した。

[実装記録](../../../artifacts/simd/task-8.4-20261018T202312Z/status-report.md)と[独立レビュー](../../../artifacts/simd/review-8.4-20261018T203820Z/review-verdict.md)はREADY_FOR_REVIEW/APPROVED。全22宣言の同一908source・75件のID/引数/期待値でOFF全失敗・終了2→明示ON全成功・終了0、全フラグ除去後も全成功。警告・エラー0のReleaseビルド後、4785/160/702件全成功・失敗/skip0・各終了0。最終12hash・既存改行・整形・差分検査も一致した。旧期待の指定9ファイルと追加1ファイルについて、診断再走査・元位置・検査順・非実行・import未確認範囲を維持し、未対応状態だけのケースを廃止した理由を[引継ぎ](../../../artifacts/simd/task-8.4-20261018T202312Z/old-expectations-handoff.md)へ記録した。汎用unsupported分類・公開例外・宣言判定・例外変換は保持した。

[公式監査](../../../artifacts/simd/task-8.4-20261018T202312Z/results-audit-classified.json)は147入力53907command、passed52830・out_of_scope1077、未対応/blocked/failed/runner_error/回帰/欠落/未比較/未処理0。元27482件と直前52027件のpassedを全保持し803件が新たに成功した。全期待値・147source/5821素材・出典/profileは不変。v128結果23573件と型だけの期待を持つtrap50件を区別して監査し、既存matcherを使用した。初回補助監査のassert_return限定仮定と初回analyzer失敗は解消し原ログも保持した。親は原ログ・終了値・最終hashと独立監査を直接確認し、kiro-verify-completionをVERIFIEDとした。共通経路9.x、CPU支援無効、最終verifyとbaseline保存は後続範囲で、機能全体GOは含めない。


## 9.1 共通値経路とホスト連携

製品変更なしの適合確認として、既存のv128引数・複数結果・localゼロ初期値・直接call・return・block/if/分岐・両形式host・typed/untyped selectを再利用し、不足するloop/backedge/drop2件、別module間接call1件、引数/結果型不一致2件、再export1件、同期再入3件を追加した。上下64bit・型と順序、所属/呼び出し元instance、関数の同一性、外側operand/local、ホスト例外同一性と返却済み結果を確認する。[項目別対応](../../../artifacts/simd/task-9.1-20261018T204713Z/coverage.md)では、新規v128再入の成功2件/ホスト例外1件と、再利用したscalarのtrap・exhaustion・不正host結果・別instanceの上限復元を区別した。

非behavioralでRED=N/A。警告・エラー0のReleaseビルド後、新規9件・再利用50件・runtime4794件が全成功・失敗/skip0・各終了0。[独立レビュー](../../../artifacts/simd/review-9.1-20261018T205414Z/review-verdict.md)はAPPROVEDで、同じ全件結果に9/50件が含まれることも確認した。変更は既存テスト1ファイルへの402行追加だけ。CRLF維持、整形・差分・最終hash一致を確認し、親のkiro-verify-completionはVERIFIED。start/global/共有memoryと最終受入は後続範囲。


## 9.2 globalと共有memory・start

製品変更なしの適合確認で、global共有1件・memory共有1件と既存リンク型/可変性のv128パラメータ2件を追加した。globalはimmutable/mutable import/exportの同一性、guest get/set→host/別instance、host更新→guest、保存済み値と定義globalのinstance独立を確認。memoryはdataの全16byte、grow後ゼロ、scalar/SIMD/host更新、2instanceとhostの同一性、grow前data保持を確認した。既存のglobal初期化・定数式/型/可変制約と8.4のInvoke/starttrap2件など13メソッド38件を再利用し、失敗store未変更・先行副作用・保存参照・次callを維持した。

[担当記録](../../../artifacts/simd/task-9.2-20261018T210136Z/self-review.md)はRED=N/Aの非behavioral確認。警告・エラー0のビルド後、追加filter6件（新規4/旧リンク2）・再利用38件・runtime4798件が全成功・失敗/skip0・終了0。[独立レビュー](../../../artifacts/simd/review-task-9.2-20261018T211000Z/review-verdict.md)はAPPROVEDで、全件TUnit JSONから同じ44件の成功を確認し、期待byteも独立計算した。変更3テストの既存改行、最終hash、整形/差分検査は一致。親は全差分・原ログ・現在hashを照合し、kiro-verify-completionをVERIFIEDとした。公式最終受入とCPU支援無効の確認は10.xへ引き継ぐ。


## 10.1 命令集合・規則対応と局所検証

HEAD`10d744753ea32d12721251c0b9901512ba09004e`で製品とテストを変更せず確認した。51受入基準の固定規則・実装・既存テストへの対応は[coverage.md](coverage.md)。以下の監査・実行記録は`artifacts/simd/task-10.1-20261018T211244Z/`に保存した。

### 命令集合・生成metadata

固定仕様submoduleのcommitは`05ca4182176763112561ae20153975c12bd689e4`、WABTは`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`で一致した。固定付録`document/core/appendix/index-instructions.py`とmacroを読み、Releaseビルド済みDLLからreflectionで取得した生成metadataと照合した。既存`Core2InstructionFixture`と`InstructionSet_TryGetTests`は今回の全ランタイムテストでも実行した。

- 通常183、FC18、FD236命令の番号・名前が一致し、FDの未接続・欠落・名前不一致は0。全437宣言に`ImmediateKind.Unsupported`は残らない。
- FDは236宣言→236種類の`ExecutionOpcode`→226種類のhandler。生成器の`ExecutionOpcode`はprefix/codeごとの識別子で、メモリのsplat・zero・laneではhandlerを共有する。従来引継ぎの「distinct FD実行opcode226」はhandler数との取り違えだったため、今回の生成metadataと`InstructionGenerator.cs:646`に従って訂正した。236種類のhandlerを要求するという意味ではない。
- 00〜FFの20欠番は`9A A2 A5 A6 AF B0 B2 B3 B4 BB C2 C5 C6 CF D0 D2 D3 D4 E2 EE`で、metadataにもない。範囲外番号と不正LEBも既存Decodeテストで拒否する。
- `metadata-extract.ps1`は生成された`Interpreter.RunLoop`のILを命令境界に従って解読し、全226handlerへのcallを確認した。`instruction-audit.json`に全236命令の即値・型・幅・lane制約・固定規則アンカー・handlerを記録し、`handler-test-index.json`に既存225handlerテストクラスと公開v128.constテストの対応を記録した。命令数とcallの存在だけを意味論や最終合格の根拠にはしない。

### 指定9ファイルの旧未対応期待

表のパスは`tests/WasmSharp.Tests/`からの相対パス。8.4の`old-expectations-handoff.md`を参照した後、下記の現在のテスト本文を直接読み、今回の4,798件の全件実行で再確認した。今回のタスクではテストを再編集していない。

| ファイル | 維持した独立した目的と現在の確認 | 廃止した観測と理由 |
| --- | --- | --- |
| Modules/ExpressionDecoder_ReadTests.cs | 「メモリ命令の後に不正がある」はBounded/Diagnosticの両モードで完全なv128.loadのmemargを読み、後続のillegal opcodeを位置46・function3・section10で報告する。既存の通常/診断再走査の境界処理も保持する。 | 全FD命令接続により、当該入力の「未対応後を走査しない」「2未確認範囲」という観測は生じないため廃止した。reader.Unsupportedと公開例外の汎用契約は残す。 |
| Modules/ModuleDecoder_DecodeFailureTests.cs | 「メモリ命令の即値が切断される」は旧FD0105/FD00をDecodeの境界診断・物理終端・function0/section10として確認する。「未割当opcodeか不正LEBか平坦else」はFD9A01/FD8002等を破損として拒否する。 | 未対応機能・即値前の打切り・未確認範囲は接続後の同じ入力では発生しない。未割当を未対応に読み替えて代用しない。 |
| Modules/ModuleDecoder_DecodeInstructionTests.cs | 「有効なcall即値の後にメモリ即値が切断される」で複数byteのcall添字を読了後、後続FD00の切断を元入力の物理終端・function0/section10で返す。 | 後続メモリ命令の機能名・未確認範囲の期待は、正常に接続されたため廃止した。複数byte即値後の診断位置という目的は保持する。 |
| Modules/ModuleDecoder_ReadElementTests.cs | 3.1で更新された「要素式に完全なV128定数がある」で、両読取モードの完全16byteと元位置、elementのoffset/initializerの要求型不一致をValidateでtype mismatch、section9・元終端位置、Instantiate不可として確認する。切断のDecode診断も別テストで保持する。 | 完全なv128.constは対応命令になったため、即値前の未対応という期待を廃止した。Decodeと要求型の検証を分離する。 |
| Modules/ModuleValidator_ValidateSelectTests.cs | 3.1で更新されたtyped/untypedの候補/条件の型不一致、余剰値、到達不能と再検証を保持する。「V128のselect候補か条件の型が不一致」は2回のValidate失敗、元select位置、FunctionCodes空、Instantiate不可を確認する。 | v128 selectの明示的な未対応分岐は接続時に除去した。正常なselectや型不一致を未対応としていた期待は保持しない。 |
| WasmModule_DecodeTests.cs | 「診断再走査で境界後の命令に到達する」はbyte/Streamで完全な即値と切断を区別し、元位置19/38等のDecode診断を確認する。「公開Decodeでメモリ即値が切断される」も両入口の境界診断を保持する。 | 未対応による再走査打切り、機能名・範囲の期待は、全命令接続後の同じ入力では存在しないため廃止した。診断の再走査自体は残す。 |
| WasmModule_DecodeDataTests.cs | 「完全なメモリ式と宣言件数違反がある」で関数件数→データ件数を保持する。「データに完全なメモリ式がある」は整合時にDecode後のValidateでconstant expression requiredとInstantiate不可を確認する。「データの宣言範囲外でメモリ即値が切断される」は再走査でも元終端を保持する。「完全なメモリ命令の後にデータ構文違反がある」は件数を推測せず構文診断を先に返す。 | 未対応時だけの早期件数照合、後続を走査しないという観測は同じ入力で成立しないため廃止した。読了後の検査順と定数式の規則を継続する。 |
| WasmModule_DecodeGlobalsTests.cs | 3.1で更新された完全v128.constの未評価bit保持と要求型不一致、切断を両入力で確認する。「GlobalのV128定数が要求型と異なる」は2回のValidateでtype mismatch・位置31/section6・FunctionCodes空・Instantiate不可。 | 完全な定数の未対応という期待は接続により廃止した。即値切断と要求型不一致はそれぞれDecode/Validateへ移した。 |
| WasmModule_InspectImportsTests.cs | 「メモリ命令とstartと巨大limitsがある」で生成・実行を要求せず全import種類を取得する。「メモリ命令とデータにSIMDが併存する」はconst/shuffle/loadそれぞれの完全入力をbyte/Streamで調査し、Decode/Validate後もcallback0・memory99を確認する。code/dataを含む6未確認範囲と全体Validate未実施範囲を保持し、Instantiate時に初めてcallback1・memory42になる。 | 未対応本体を前提とした旧名、現状到達しないunsupported枝の期待、意味のないnull local等は接続時に除去した。InspectImportsの未確認範囲と非実行は現在も正常入力から到達するため保持した。 |

追加の`WasmModule_DecodeDataInstructionTests.cs`も確認した。「データ参照後に完全なメモリ式がある」の旧3期待は、`data.drop`を含むコードとデータ式で、構文読了後に関数件数→データ件数→data count section必須の順を検査する目的を保持する。8.4で完全なmemarg/endへ更新し、目的外の後続不正sectionを除き、同じ検査順・元位置を検査する。接続後に発生しなくなった未対応による打切りを廃止しただけで、製品の責務は追加していない。

### 保持した汎用unsupported経路の到達性と理由

現在の固定Core 2.0の正常入力は、通常183/FC18/FD236のいずれでも`ImmediateKind.Unsupported`を選ばない。未割当opcodeは`ExpressionDecoder.Read`でillegal opcodeとしてDecode不正になる。v128 selectの未対応分岐も除去済みで、テスト専用の未対応opcodeや注入機構はない。

次の汎用経路は設計「公開経路の統合確認」で保持を指定された既存の宣言・公開例外・分類・変換契約であり、全接続を理由に削除していない。

- `InstructionAttribute.cs`の名前だけのconstructorがUnsupportedの即値・型効果・検証規則を構成する契約。現在の固定命令宣言では使用されないが、生成器テストの未接続宣言や契約診断を扱う。
- `ExpressionDecoder.Read`のUnsupported宣言判定と`ModuleBinaryReader.Unsupported`の元位置・未確認範囲の構築。現在の固定命令入力からはこの宣言条件が成立しない。readerの構築処理はソースで確認し、`WasmUnsupportedFeatureException_ConstructorTests`は公開例外の機能・位置・未確認範囲を直接確認する。到達不能になったreader分岐のための注入や新規テストは追加しない。
- `ModuleDecoder.ResolveBoundaryFailure`と件数照合周辺の`catch(WasmUnsupportedFeatureException)`。現在の固定正常入力にはthrow元がないため到達しないが、既存の未対応で再走査済み診断を置き換えない契約を残す。現行の切断・後続不正・件数順は表の接続後テストで確認する。
- `ImportInspector.Inspect/InspectionFailure`のUnsupportedFeature分類と公開取得失敗への変換。現在の固定正常入力では未対応例外の変換分岐に到達しない。`WasmImportInspectionException_ConstructorTests`等とランナー`CaseDiagnostic_FromExceptionTests`は、公開例外の機能・位置・未確認範囲を扱う契約を直接確認する。
- ランナーの`CaseDiagnostic.FromException`、`AssertionJudge.Judge`、`ScriptExecutor`のruntime_unsupported/blocked分類は公開未対応例外を受け取る汎用契約として保持する。既存`CaseDiagnostic_FromExceptionTests`と`AssertionJudge_JudgeTests`を今回の702件全件で実行した。現在の固定Core 2.0ランタイムに未接続命令が残ることを意味しない。

InspectImportsの`UnverifiedRanges`はunsupportedと同義ではなく、調査対象外payloadと全体Validate未実施の通知である。この範囲は現在の正常入力でも到達可能で、上記mixedテストで6範囲を直接確認する。未対応例外の注入によって到達性を作ることはしていない。

### Releaseビルドと3プロジェクト

すべて順次実行し、ビルド成功・警告0・エラー0を確認してからテストへ進んだ。UTC・argv・終了値は各`*-command.json`、全出力は同名の`.log`に記録した。製品とテストソースは開始時から不変である。今回の作業は非behavioralでRED=N/A、新規テストはない。

| コマンド | 記録 | 結果 |
| --- | --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --warnaserror` | build-command.json/build.log | exit0、警告0・エラー0 |
| `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build` | runtime-all-command.json/runtime-all.log | 4,798件成功、失敗0・スキップ0、exit0 |
| `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build` | generator-all-command.json/generator-all.log | 160件成功、失敗0・スキップ0、exit0 |
| `dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build` | runner-all-command.json/runner-all.log | 702件成功、失敗0・スキップ0、exit0 |

### CPU支援を無効にした子プロセスの確認

通常環境の`--list-tests`で805件を発見してから、2026-10-18T21:15:48.6004306Z〜21:15:52.3230903Zに新しいdotnet子プロセスだけへ`ProcessStartInfo.Environment["DOTNET_EnableHWIntrinsic"]="0"`を設定し、一度実行した。コマンドは次のとおりで、通常実行と同じテストと期待値を使う。

```text
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/(Interpreter_I8x16*)|(Interpreter_I16x8*)|(Interpreter_I32x4*)|(Interpreter_I64x2*)|(Interpreter_F32x4*)|(Interpreter_F64x2*)|(Interpreter_V128*)/*'
```

- 環境: Microsoft Windows 10.0.26300、x64、Intel64 Family 6 Model 183 Stepping 1/GenuineIntel。TUnitレポートのテストランタイムは.NET 10.0.13。起動に使用したPowerShell自体の.NET 10.0.11とは区別する。`dotnet-info.log`にSDK/ランタイム一覧を保存した。
- 発見805件、実行805件成功・失敗0・スキップ0・exit0。`hw-discovery.log`、`hw-disabled.log`、各command JSON、上書き前にコピーした`hw-disabled-test-results/`のTUnit JSON/HTMLが対応する。`handler-test-index.json`と同じ225クラスの集合である。
- 親プロセスの`DOTNET_EnableHWIntrinsic`はbefore/afterとも未設定、child要求値は`0`。親環境、ユーザー環境、マシン環境へ設定を書き込まず、後続プロセスに持ち越していない。
- これは設計で定めたJITのHWIntrinsic設定による確認である。BCLや事前コンパイル済みコードを含むCPU命令の全面無効化、異なるCPU/OSでの確認を意味しない。

今回の固定条件のhashは`instruction-audit.json`、実行source hashの開始/終了照合は`preflight.json`/`postflight.json`へ保存する。8.4の既存公式run監査は参照したが、10.2の単一最終run/compare/verify、10.3のbaseline-saveはこのタスクでは実行していない。8系の最終判定と保存はローカル要件/ADRの適合運用であり、Wasm規範にbaseline保存規則があるようには扱わない。タスク10.1の局所検証をSIMD機能全体またはCore 2.0全体のGOには読み替えない。


[独立レビュー](../../../artifacts/simd/review-10.1-20261018T212841Z/review-verdict.md)はAPPROVED。独立ビルド警告/エラー0、4798/160/702件全成功・失敗/skip0・全終了0。HW無効805件は再実行せず、保存結果・discovery・通常全件のID・source/DLLhashで同一性を確認した。親は全51行と旧期待/汎用経路の記録、原ログ・終了値・現在979hashを直接確認し、kiro-verify-completionをVERIFIEDとした。未解決指摘はなく、単一最終run/verifyとbaseline保存へ進む。
