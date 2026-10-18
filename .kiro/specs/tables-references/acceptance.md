# tables-references受入記録

## 1.1 固定素材と既存テスト基盤の確認

2026-10-18に、HEAD`0a51bbcb0534107ca5fa4418caff6f93aaac22d4`で準備タスク1.1を確認した。
Releaseビルドは警告・エラー0、既存3プロジェクトのテストは合計4,084件成功だった。
本記録は実装着手時の前提確認であり、対象機能の受入判定は後続タスクで行う。

### Task Brief

| 項目 | 内容 |
| --- | --- |
| 受入基準 | 固定profile・素材・直前の受入済みbaselineを識別し、既知の診断差をケース単位で特定する。既存fixtureの能力と、Releaseビルド後の既存テスト成立を確認する。 |
| 完了条件 | 本記録と、新規[確認記録ディレクトリ](../../../artifacts/tables-references/task-1.1-20261018T020426Z/)にコマンド・UTC・終了値・ログ・TRX・素材監査を保存する。 |
| 設計制約 | ConformanceVerificationの範囲で既存CLIとfixtureを使う。linear-memory最終保存を比較元とし、baselineと固定素材を保持する。 |
| 検証方法 | hashとprofileの照合、保存済み結果間の既知差照合、警告をエラー扱いするReleaseビルド、3つのTUnitプロジェクト、CLIの`--help`を順に確認する。 |

対象要件は[requirements.md](requirements.md)の9.1、9.3、9.5である。
参照した設計見出しは[ConformanceVerification](design.md#conformanceverification)、[検証方針（Testing Strategy）](design.md#検証方針testing-strategy)、[ファイル構成計画（File Structure Plan）](design.md#ファイル構成計画file-structure-plan)である。
9.1と9.3の全体実行・回帰判定は機能接続後に行い、今回は素材と比較元の確認までを扱う。

### 固定素材と比較元

比較元は[linear-memoryの明示baseline保存](../linear-memory/acceptance.md#84の明示baseline保存)に従って選んだ。
[素材監査](../../../artifacts/tables-references/task-1.1-20261018T020426Z/prerequisites-audit.json)で以下を実ファイルから再確認した。

| 対象 | 識別情報 |
| --- | --- |
| 固定Core 2.0仕様 | `05ca4182176763112561ae20153975c12bd689e4` |
| 固定WABT | `c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`。1.0.42とDataCount修正 |
| manifest | [wabt-commit-20261016/corpus/manifest.json](../../../artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json) |
| manifestのSHA-256 | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 受入済みbaseline | [task-8.4-20261016T001148Z/run-baseline.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/run-baseline.json) |
| baselineのSHA-256 | `70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1` |
| 素材生成に使った変換器 | `artifacts/wabt-datacount-tests/Release/wast2json.exe` |
| 変換器のSHA-256 | `2c9114cb9fc457c323f0d45eb507614a9530180b4b7a656bd9914823ff8a31c8` |

specとWABTの作業コピーのHEADは上記固定commitと一致した。
[現行profile](../../../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)とmanifestのprofile、baseline内のcorpusとmanifestは、それぞれ全体が一致した。
profileは全21機能のうち7機能がON、14機能がOFFで、147入力にSIMDの57入力を含む。
固定specのGit blobから147入力のSHA-256を確認し、manifestが参照する5,821素材の存在とSHA-256も確認した。
内訳はJSON147件、Wasm4,597件、WAT1,077件である。

変換済みJSONの53,907commandとbaselineの全ケースは、入力の相対pathとcommandの添字による識別子・順序が一致し、重複はなかった。
manifestとbaselineはいずれも処理・出力が完了していた。
baselineのケースを再集計した結果は以下のとおりである。

| 分類 | 件数 |
| --- | ---: |
| passed | 23,050 |
| failed | 0 |
| runtime_unsupported | 1,457 |
| runner_error | 0 |
| out_of_scope | 1,077 |
| blocked | 28,323 |
| 合計 | 53,907 |

### 2026-10-17の既知診断差

出典は[linear-memoryの受入記録の追記](../linear-memory/acceptance.md#2026-10-16の実装検証)、[レビュー対応の検証記録](../linear-memory/tasks.md#claude-codeレビュー対応2026-10-17)、[元の全件照合](../../../artifacts/linear-memory/claude-review-20261017-4aa3bd52/diagnostic-delta-audit.json)である。
保存済み[レビュー後のrun](../../../artifacts/linear-memory/claude-review-20261017-4aa3bd52/run.json)と今回の比較元baselineを読み取り、下記の33件だけが異なることを再確認した。
commandの添字は0始まりである。

| 入力 | commandの添字 | 件数 |
| --- | --- | ---: |
| data.wast | 9、10、47、49、50、51、52、53、54、55、56、57、58、59、60 | 15 |
| global.wast | 63〜80 | 18 |

全33件で診断message以外のケース情報が一致し、分類は変更前後ともpassedだった。
期待診断に対する前方一致とコロンまでの診断prefixを維持し、位置、段階、例外型も変わらなかった。
[保存済み比較結果](../../../artifacts/linear-memory/claude-review-20261017-4aa3bd52/comparison.json)は、回帰・追加・欠落・未比較・failed・runner_errorが各0件だった。

[今回の素材監査](../../../artifacts/tables-references/task-1.1-20261018T020426Z/prerequisites-audit.json)には、出典3ファイルのSHA-256と、全33件の識別子・期待診断・変更前後のmessage・位置・例外型を保存した。
後続の比較ではこの内容と照合し、33件という件数だけで既知差と判断しない。

### 既存fixtureの確認

[HostLinkingModuleBinary](../../../tests/WasmSharp.Tests/Fixtures/HostLinkingModuleBinary.cs)の既存操作で、後続タスクの入力を構成できる。

| 必要な入力 | 既存操作 |
| --- | --- |
| funcref・externrefの型、table、global、locals、式 | `Types`、`TypeVector`、`Tables`、`Globals`、`Code`へ型バイトや式を渡す。 |
| flags0〜7のelement、空section、不正flags・終端・宣言長 | `Section(9, payload, declaredLength)`へ件数とelementのrawバイト列を渡す。 |
| call_indirect、table.init、table.copyの2添字 | `Unsigned`をバイナリ仕様の添字順に2回配置し、`Code`へ渡す。 |
| 既存の直接callの内部実行 | [ExecutionInstanceFixture](../../../tests/WasmSharp.Tests/Fixtures/ExecutionInstanceFixture.cs)を使う。 |

`Section`と`Code`は入力の不足値や終端を補正しない。
型バイト、複合即値、非最短・切断した整数、宣言長等を保持する既存の[fixtureテスト](../../../tests/WasmSharp.Tests/Fixtures/HostLinkingModuleBinary_CreateTests.cs)も、今回のランタイム全テストに含めて実行した。
追加の共通補助は不要と判断し、C#とテストを変更しなかった。
そのため、機能フラグとREDは適用していない。

### ビルドと既存テスト

実行環境はWindows x64、.NET SDK10.0.402、Microsoft.NETCore.App10.0.13である。
[環境情報](../../../artifacts/tables-references/task-1.1-20261018T020426Z/dotnet-info.stdout.log)に実測値を保存した。
3つのテストプロジェクトはすべて`net10.0`、TUnit1.66.16を使用している。

次のコマンドをリポジトリルートで、記載順に実行した。
各`*.result.json`に正確なコマンド、UTC、終了値を、同名の`*.stdout.log`と`*.stderr.log`に出力を保存した。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/tables-references/task-1.1-20261018T020426Z/runtime
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/tables-references/task-1.1-20261018T020426Z/generators
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/tables-references/task-1.1-20261018T020426Z/runner
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe --help
python artifacts/tables-references/task-1.1-20261018T020426Z/audit-prerequisites.py
```

| 検証 | 結果 | 確認記録 |
| --- | --- | --- |
| Releaseビルド | 終了0、警告0、エラー0 | [build.result.json](../../../artifacts/tables-references/task-1.1-20261018T020426Z/build.result.json) |
| ランタイム | 終了0、3,289件成功、失敗・スキップ各0 | [実行記録](../../../artifacts/tables-references/task-1.1-20261018T020426Z/runtime.result.json)、[TRX](../../../artifacts/tables-references/task-1.1-20261018T020426Z/runtime/WasmSharp.Tests_net10.0_x64.trx) |
| 生成器 | 終了0、93件成功、失敗・スキップ各0 | [実行記録](../../../artifacts/tables-references/task-1.1-20261018T020426Z/generators.result.json)、[TRX](../../../artifacts/tables-references/task-1.1-20261018T020426Z/generators/WasmSharp.Generators.Tests_net10.0_x64.trx) |
| 公式ランナー | 終了0、702件成功、失敗・スキップ各0 | [実行記録](../../../artifacts/tables-references/task-1.1-20261018T020426Z/runner.result.json)、[TRX](../../../artifacts/tables-references/task-1.1-20261018T020426Z/runner/WasmSharp.TestSuiteRunner.Tests_net10.0_x64.trx) |
| CLI起動 | 終了0 | [help.result.json](../../../artifacts/tables-references/task-1.1-20261018T020426Z/help.result.json) |
| 素材・比較元・既知差の監査 | 終了0 | [audit.result.json](../../../artifacts/tables-references/task-1.1-20261018T020426Z/audit.result.json)、[監査スクリプト](../../../artifacts/tables-references/task-1.1-20261018T020426Z/audit-prerequisites.py) |

通常のコマンド実行はプロセス作成前に`helper_unknown_error: setup refresh had errors`となったため、上記は`sandbox_permissions: require_escalated`で実行した。
これは環境側の起動失敗であり、ビルド・テストの失敗には数えていない。

今回、公式全体のrun、compare-run、baseline-save、WABTのビルド・テスト、素材の再生成は行っていない。
既知差の確認は保存済み結果の照合であり、新しい公式実行結果ではない。
素材監査の最後にも上記manifestとbaselineのSHA-256が変わらないことを確認した。

## 6.1 参照命令と参照定数式の接続

2026-10-18に、開始時HEAD`ca6ddaa4487e383e2d4443db7eec1c987a2005fd`から参照命令と参照typed selectを公開4段階へ接続した。
`ref.null`は既存の参照型読取りを共有して型別nullを保持し、3命令の宣言を既存の検証規則とhandlerへ対応付けた。
参照typed selectの未対応分岐を除去し、型vector長1、候補2個、i32条件、untyped参照禁止、v128未対応の検査を維持した。
global定数式は準備済みの検証と関数先行構築を利用する。

### Task Brief

| 項目 | 内容 |
| --- | --- |
| 受入基準 | 型別null、関数の同一性、参照のlocals・global・引数・複数結果・if・typed selectを公開APIで確認する。宣言不足、未知関数、型注釈とoperandの不正、参照定数式の制約と診断順をValidateで拒否する。 |
| 完了条件 | 命令宣言、参照型即値、既存validatorとhandlerを接続し、公開経路のテストと[確認記録](../../../artifacts/tables-references/task-6.1-20261018T043822Z/)を保存する。 |
| 設計制約 | 既存の参照型読取り、型スタック、定数式評価、関数構築、実行handlerを利用する。table命令、section9、間接callの対応切替は後続タスクに残す。 |
| 検証方法 | FeatureFlag OFFの実行RED、ONの全件成功、除去後のビルドとランタイム・生成器全件、公式147入力のrunと同じ保存結果のcompare-run、ケース単位監査、整形と差分検査を行う。 |

対象要件は1.1、1.3、1.4、2.2、2.4、2.6、2.7、3.1〜3.6、6.1、8.5である。
設計の「InstructionDefinitionsとModuleDecoder」「ReferenceValidationとConstantInitializer」「InstanceConstruction」「ReferenceExecutionとTableExecution」「ConformanceVerification」「責務境界」「ファイル構成計画」「検証方針」を照合した。
6.1と8.5のうち、要素とtable等の公開接続は後続タスクで扱う。

### 公開経路とTDD

| 確認範囲 | テスト |
| --- | --- |
| 両参照型のnull、追加localの初期値、null判定、参照の選択・転送、globalと命令が返す関数の同一性とinstance分離 | [WasmFunction_InvokeReferenceTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeReferenceTests.cs) |
| import immutable参照globalの定数式、globalだけで宣言したimport関数の参照 | [WasmModule_InstantiateReferenceIntegrationTests.cs](../../../tests/WasmSharp.Tests/WasmModule_InstantiateReferenceIntegrationTests.cs) |
| 関数本体/start/importだけでは未宣言、未知関数の優先、到達不能、参照select、定数性→global適格性→参照→結果の診断順、定義/mutable globalの拒否 | [WasmModule_ValidateReferenceTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateReferenceTests.cs) |
| 不正reftype、整数の長さと上限、即値や終端の欠落、入力位置 | [WasmModule_DecodeReferenceTests.cs](../../../tests/WasmSharp.Tests/WasmModule_DecodeReferenceTests.cs) |
| 通常解析と診断走査での型別null、最大関数添字をDecodeでは検証しないこと、入力順と位置 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs) |

FeatureFlag OFFで警告・エラー0のReleaseビルド後に新規の公開経路43件を実行し、39件失敗、4件成功を確認した。
[REDログ](../../../artifacts/tables-references/task-6.1-20261018T043822Z/red-tests.log)と[TRX](../../../artifacts/tables-references/task-6.1-20261018T043822Z/red/WasmSharp.Tests_net10.0_x64.trx)に、参照機能の未対応を主因とする失敗を保存した。
ONでランタイム3,548件が成功し、フラグ除去後も同じ3,548件と生成器106件が成功した。
各段階のsource hashを同じディレクトリに保存した。

追加テストの`D070`はref.nullの即値直後で入力が終わるため、当初の期待`END opcode expected`を`unexpected end of section or function`へ補正した。
既存の`ModuleBinaryReader.ReadByte`が`ReadBytes(1)`を呼び、物理EOFを同prefixで報告することに合わせたもので、入力と製品の終端処理は変更していない。

| 検証 | 結果 | 記録 |
| --- | --- | --- |
| Releaseビルド | 終了0、警告0、エラー0 | [final-build.log](../../../artifacts/tables-references/task-6.1-20261018T043822Z/final-build.log) |
| ランタイム全件 | 終了0、3,548件成功、失敗・スキップ各0 | [runtime-tests.log](../../../artifacts/tables-references/task-6.1-20261018T043822Z/runtime-tests.log)、[TRX](../../../artifacts/tables-references/task-6.1-20261018T043822Z/runtime/WasmSharp.Tests_net10.0_x64.trx) |
| 生成器全件 | 終了0、106件成功、失敗・スキップ各0 | [generator-tests.log](../../../artifacts/tables-references/task-6.1-20261018T043822Z/generator-tests.log)、[TRX](../../../artifacts/tables-references/task-6.1-20261018T043822Z/generator/WasmSharp.Generators.Tests_net10.0_x64.trx) |

### 公式全体実行と比較

1.1と同じmanifestとbaselineを使い、全147入力、53,907commandを実行した。
固定specとWABTのHEAD、manifestとbaselineのSHA-256は1.1の値と一致した。
[run.json](../../../artifacts/tables-references/task-6.1-20261018T043822Z/run.json)の保存結果を再実行せず[comparison.json](../../../artifacts/tables-references/task-6.1-20261018T043822Z/comparison.json)で比較し、両操作とも終了0だった。

| 分類 | 件数 |
| --- | ---: |
| passed | 23,072 |
| failed | 0 |
| runtime_unsupported | 1,447 |
| runner_error | 0 |
| out_of_scope | 1,077 |
| blocked | 28,311 |

比較は変化72件、追加・欠落・回帰・未比較各0件、条件・出典・素材差各0件だった。
[ケース監査](../../../artifacts/tables-references/task-6.1-20261018T043822Z/results-audit.json)で既知の33件を識別子ごとに照合し、診断message以外の項目はbaselineと一致し、変更後のケース全体は2026-10-17の保存runと一致した。
残る39件の内訳は新規passed22件と、未対応位置が参照命令から後続機能へ進んだ17件だった。
監査は保存済みJSONだけを読む[audit-results.py](../../../artifacts/tables-references/task-6.1-20261018T043822Z/audit-results.py)で再確認できる。
出力先を省略するとUTC付きの新規ファイルへ保存し、指定したファイルが既に存在する場合は拒否する。
以下のコマンドで[再確認結果](../../../artifacts/tables-references/task-6.1-20261018T043822Z/results-audit-recheck.json)を別途保存し、初回監査と内容が一致することを確認した。

```powershell
python artifacts/tables-references/task-6.1-20261018T043822Z/audit-results.py --output artifacts/tables-references/task-6.1-20261018T043822Z/results-audit-recheck.json
```

| 新規passedの入力 | commandの添字（0始まり） |
| --- | --- |
| data.wast | 48 |
| linking.wast | 31〜37 |
| ref_func.wast | 13、15、16 |
| ref_is_null.wast | 14、15 |
| ref_null.wast | 0〜2 |
| unreached-invalid.wast | 117 |
| unreached-valid.wast | 0〜4 |

`ref.null`、`ref.func`、`ref.is_null`、参照typed selectを直接理由とするruntime_unsupportedは0件だった。
`ref_func.wast`の2・14、`ref_is_null.wast`の0、`global.wast`の0・106、`select.wast`の0はsection.element、`select.wast`の147はcall_indirectが未対応である。
同入力のblockedは監査JSONに各commandの起因となるケース識別子と未対応機能を保存した。
未対応位置が進んだ17件はsection.elementが3件、table.fillが5件、table.growが6件、table.setが3件であり、それぞれ後続の接続タスクで確認する。

今回の結果は6.1の接続範囲を確認したもので、Core 2.0全体やtables-references全体の完成判定ではない。
baseline-save、固定素材の再生成、公式ランナーの単体テストは行っていない。
