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

## 8.1 対象規則と公開契約の確認

2026-10-18にHEAD`915aa6638780f6e2d5543770d84e832187531125`で確認した。
製品・テストは変更せず、既存の補助テストと固定仕様を照合した。新たに補う必要のある規則は見つからなかった。
本節は8.1の確認であり、8.2の最終全体実行、8.3のケース受入、8.4のbaseline保存は後続タスクで行う。

### Task Brief

| 項目 | 内容 |
| --- | --- |
| 受入基準 | 55受入基準を固定仕様の規則・実装・公式ケースまたは補助テストへ対応付け、対象の未対応期待と公開契約の回帰がないことを確認する。 |
| 完了条件 | 原番号の55行対応表、公開ホスト契約・OOMの確認範囲、最新3プロジェクトの検証結果、後続受入との区別を本節と[確認記録](../../../artifacts/tables-references/task-8.1-20261018T082940Z/)へ保存する。 |
| 設計制約 | ConformanceVerificationの範囲で既存APIとランナーを使用する。固定素材・期待値・分類・比較元を変更せず、実際のOOMを起こすための大量割当や注入口を追加しない。 |
| 検証方法 | 固定仕様とソース・テストの照合、55基準とリンクの機械確認、Releaseビルド後のruntime・generator・runnerの順次実行、既存7.2結果の再読監査、整形・空白検査・CLIのhelpを確認する。 |

### 55受入基準の対応

固定仕様は1.1と同じcommit`05ca4182176763112561ae20153975c12bd689e4`である。
規則名はリンク先のRSTラベルに対応する。分類・公開API・診断文・受入手順はCore仕様の規定ではないため、その行には本仕様またはADRの契約も併記した。
「補助成功」は今回の3プロジェクト実行でリンク先の既存テストが成功したことを表し、最終公式受入の完了を意味しない。
補助テストのリンクは該当メソッドの宣言行を示す。完全なメソッド名は[照合スクリプト][tr-check]による[照合結果][tr-coverage]の各行にも記録する。

| 要件ID | 固定仕様規則の出典 | 実装 | 公式または補助テスト | 確認状態 |
| --- | --- | --- | --- | --- |
| 1.1 | [binary-instr][tr-bin-instr] | [InstructionSet][tr-def]・[ExpressionDecoder.Read][tr-decode] | [参照即値と位置][tr-t-ref-decode]・[間接callのtype→table][tr-t-call-decode]・[一括table即値][tr-t-bulk-decode] | 補助成功 |
| 1.2 | [binary-elemsec][tr-bin-module] | [ModuleDecoder.ReadElement][tr-element-decode] | [全形式・非最短u32][tr-t-flags]・[全flagsの公開初期化][tr-t-element-modes] | 補助成功 |
| 1.3 | [binary-elemsec][tr-bin-module]・[binary-instr][tr-bin-instr] | [ModuleDecoder.ReadElement][tr-element-decode]・[ExpressionDecoder.Read][tr-decode] | [不正値・符号化][tr-t-element-syntax]・[式の切れ][tr-t-element-eof]・[参照即値の破損][tr-t-ref-malformed] | 補助成功 |
| 1.4 | [valid-elem][tr-valid-module]・[valid-constant][tr-valid-instr] | [ReadElement][tr-element-decode]→[ValidateElements][tr-element-validate] | [静的不正はDecodeで保持][tr-t-element-static]・[Validate失敗後のInstantiate拒否][tr-t-element-invalid] | 補助成功 |
| 1.5 | [binary-instr-vec][tr-bin-instr]・[本仕様1.5][tr-req] | [ExpressionDecoder.Read][tr-decode]・[v128のselect未対応][tr-v128] | [公開Decodeの未対応診断][tr-t-unsupported]・[未確認範囲の保持][tr-t-expression-unsupported]・[v128のselect未対応][tr-t-v128] | 補助成功・SIMD維持 |
| 2.1 | [valid-instr-table][tr-valid-instr]・[valid-call_indirect][tr-valid-instr] | [FunctionValidator.Validate][tr-validator] | [一括tableの添字・型・operand][tr-t-bulk-invalid]・[間接callの静的検査][tr-t-call-invalid] | 補助成功 |
| 2.2 | [valid-ref.func][tr-valid-instr]・[valid-module][tr-valid-module] | [ReferenceValidationContext.Create][tr-context]・[FunctionValidator][tr-validator] | [関数本体・start・importは宣言にならない][tr-t-ref-declaration]・[全modeの宣言収集][tr-t-declarations] | 補助成功 |
| 2.3 | [valid-elem][tr-valid-module]・[valid-elemmode][tr-valid-module] | [ModuleValidator.ValidateElements][tr-element-validate] | [要素型・offset・添字の拒否][tr-t-element-invalid] | 補助成功 |
| 2.4 | [valid-constant][tr-valid-instr]・[valid-global][tr-valid-module] | [ConstantInitializer.Validate][tr-constant] | [定数性と参照の診断順][tr-t-constant]・[定義global・mutable import拒否][tr-t-element-global]・[immutable参照global][tr-t-import-global] | 補助成功 |
| 2.5 | [valid-table.copy][tr-valid-instr]・[valid-table.init][tr-valid-instr]・[valid-call_indirect][tr-valid-instr] | [FunctionValidator][tr-validator] | [table→elem→型→operand][tr-t-bulk-invalid]・[table→type→funcref→operand][tr-t-call-invalid] | 補助成功 |
| 2.6 | [polymorphism][tr-valid-instr]・[valid-select][tr-valid-instr] | [FunctionValidator][tr-validator] | [Unknownの参照判定・typed select][tr-t-ref-unreachable]・[到達不能でも一括tableの静的検査][tr-t-bulk-invalid]・[間接callの静的検査][tr-t-call-invalid] | 補助成功 |
| 2.7 | [valid-module][tr-valid-module]・[本仕様2.7][tr-req] | [WasmModule.Validate][tr-module-validate] | [後半関数の失敗後も再試行・Instantiate拒否][tr-t-validation-state]・[要素の検証失敗][tr-t-element-invalid] | 補助成功 |
| 3.1 | [exec-ref.null][tr-exec-instr] | [Interpreter.PushConstant][tr-push] | [型別nullと追加local][tr-t-ref-null] | 補助成功 |
| 3.2 | [exec-ref.func][tr-exec-instr] | [Interpreter.RefFunc][tr-ref] | [globalと命令で同じ関数][tr-t-ref-identity] | 補助成功 |
| 3.3 | [exec-ref.is_null][tr-exec-instr] | [Interpreter.RefIsNull][tr-is-null] | [型別null][tr-t-ref-null]・[非null関数][tr-t-ref-identity] | 補助成功 |
| 3.4 | [exec-instr-variable][tr-exec-instr]・[exec-call][tr-exec-instr] | [Interpreter.Run・LocalGet・GlobalGet][tr-interpreter] | [引数・local・if・複数結果][tr-t-ref-control]・[追加localのnull][tr-t-ref-null]・[globalの関数同一性][tr-t-ref-identity] | 補助成功 |
| 3.5 | [exec-select][tr-exec-instr] | [Interpreter.Select][tr-select] | [typed selectで選択した参照の保持][tr-t-ref-control] | 補助成功 |
| 3.6 | [valid-select][tr-valid-instr] | [FunctionValidator][tr-validator] | [untyped参照禁止・型数・候補・条件][tr-t-select-invalid] | 補助成功 |
| 4.1 | [exec-table.get][tr-exec-instr]・[exec-table.set][tr-exec-instr] | [TableGet・TableSet][tr-table]・[WasmTable][tr-wasm-table] | [importと定義table・両参照型][tr-t-table-getset] | 補助成功 |
| 4.2 | [exec-table.get][tr-exec-instr]・[exec-table.set][tr-exec-instr] | [TableGet・TableSet][tr-table] | [符号なし添字・未変更・Invoke位置][tr-t-table-bounds] | 補助成功 |
| 4.3 | [exec-table.size][tr-exec-instr] | [TableSize][tr-size] | [共有tableのサイズと更新][tr-t-table-shared] | 補助成功 |
| 4.4 | [exec-table.grow][tr-exec-instr]・[grow-table][tr-exec-module] | [TableGrow][tr-grow]・[TryGrow][tr-trygrow] | [旧サイズ・共有追加領域][tr-t-table-shared]・[非null参照を指定した増大][tr-t-host-grow] | 補助成功 |
| 4.5 | [exec-table.grow][tr-exec-instr]・[grow-table][tr-exec-module] | [TableGrowの限定catch][tr-grow]・[TryGrowの割当後確定][tr-trygrow] | [宣言・保持上限失敗時の未変更][tr-t-table-limit]・[配列保持上限][tr-t-host-limit] | 上限成功・OOMコード確認 |
| 4.6 | [exec-table.grow][tr-exec-instr]・[本仕様4.6][tr-req] | [同じWasmTableへの操作][tr-wasm-table] | [複数instance・ホスト共有・増分0][tr-t-table-shared]・[上限で増分0][tr-t-grow-zero] | 補助成功 |
| 5.1 | [exec-table.copy][tr-exec-instr] | [TableCopy][tr-table-copy]・[TryCopy][tr-copy] | [異なる添字と共有実体の前後重複][tr-t-copy-fill] | 補助成功 |
| 5.2 | [exec-table.fill][tr-exec-instr] | [TableFill][tr-fill]・[TryFill][tr-wasm-table] | [copyとfillの参照同一性][tr-t-copy-fill] | 補助成功 |
| 5.3 | [exec-table.init][tr-exec-instr] | [TableInit][tr-init]・[ElementInstance.TryInitialize][tr-element] | [全形式の保持と破棄][tr-t-bulk-modes]・[転送後も保持するpassive][tr-t-passive] | 補助成功 |
| 5.4 | [exec-table.copy][tr-exec-instr]・[exec-table.fill][tr-exec-instr]・[exec-table.init][tr-exec-instr] | [WasmTableの範囲検査][tr-wasm-table]・[TryInitialize][tr-element] | [uint範囲・部分更新なし][tr-t-bulk-bounds] | 補助成功 |
| 5.5 | [exec-table.copy][tr-exec-instr]・[exec-table.fill][tr-exec-instr]・[exec-table.init][tr-exec-instr] | [WasmTable][tr-wasm-table]・[ElementInstance][tr-element] | [末尾の0長と末尾超過][tr-t-bulk-bounds] | 補助成功 |
| 5.6 | [exec-elem.drop][tr-exec-instr] | [ElementDrop][tr-drop]・[ElementInstance.Drop][tr-element-drop] | [反復drop・全mode][tr-t-bulk-modes]・[別instanceと転送済み参照の分離][tr-t-passive] | 補助成功 |
| 6.1 | [alloc-module][tr-exec-module]・[exec-instantiation][tr-exec-module] | [WasmInstanceの関数→global→要素構築][tr-instance] | [globalだけで宣言したimport関数][tr-t-global-function]・[要素の参照globalと関数][tr-t-element-reference]・[instanceごとの寿命][tr-t-passive] | 補助成功 |
| 6.2 | [exec-instantiation][tr-exec-module]のrunelem | [InitializeElementSegments][tr-element-initialize] | [全flagsの公開初期化][tr-t-element-modes]・[宣言順の転送と停止][tr-t-element-failure] | 補助成功 |
| 6.3 | [exec-instantiation][tr-exec-module]のrunelem | [InitializeElementSegments][tr-element-initialize]・[ElementInstance.Drop][tr-element-drop] | [passive/declarativeの寿命][tr-t-bulk-modes]・[失敗前後のdeclarative][tr-t-retained-function] | 補助成功 |
| 6.4 | [exec-table.init][tr-exec-instr]・[exec-instantiation][tr-exec-module] | [InitializeElementSegments][tr-element-initialize] | [先行成功だけ残し後続element・data・start停止][tr-t-element-failure]・[失敗後に残る関数][tr-t-retained-function] | 補助成功 |
| 6.5 | [exec-instantiation][tr-exec-module] | [Instantiateのelement→data→RunStart][tr-instantiate] | [初期化順とstartの参照][tr-t-element-data-start] | 補助成功 |
| 6.6 | [exec-instantiation][tr-exec-module]・[ADR0011][tr-adr-retain] | [ModuleInstantiator][tr-instantiate]・[WasmInstance][tr-instance] | [要素/data失敗後の共有関数とdrop順][tr-t-retained-function]・[start失敗後の保存参照][tr-t-start-failure] | 補助成功 |
| 6.7 | [exec-instantiation][tr-exec-module]のimport照合 | [InstantiateのLink先行][tr-instantiate] | [後続import不成立でも共有領域未変更・start未実行][tr-t-link-failure] | 補助成功 |
| 7.1 | [exec-call_indirect][tr-exec-instr] | [CallIndirect→CallFunction][tr-call] | [別module・複数引数結果の順序][tr-t-call-values] | 補助成功 |
| 7.2 | [exec-call_indirect][tr-exec-instr]・[参照実装eval.ml][tr-spec-eval] | [CallIndirect][tr-call]・[GetTrapMessage][tr-boundary] | [範囲外・selector・callback前のtrap][tr-t-call-trap] | 補助成功 |
| 7.3 | [exec-call_indirect][tr-exec-instr]・[参照実装eval.ml][tr-spec-eval] | [CallIndirect][tr-call]・[GetTrapMessage][tr-boundary] | [nullと添字付き診断][tr-t-call-trap] | 補助成功 |
| 7.4 | [exec-call_indirect][tr-exec-instr] | [引数型列・結果型列のSequenceEqual][tr-call] | [別moduleの構造的同型][tr-t-call-values]・[引数/結果型不一致][tr-t-call-trap] | 補助成功 |
| 7.5 | [exec-call][tr-exec-instr]・[exec-call_indirect][tr-exec-instr] | [CallFunction][tr-call-function] | [定義元global・table・memory][tr-t-call-values] | 補助成功 |
| 7.6 | [exec-call_indirect][tr-exec-instr]・[ADR0007][tr-adr-host]・[ADR0010][tr-adr-instance] | [CallFunction→RunHost][tr-call-function] | [呼び出し元instanceと同じホスト例外][tr-t-call-host] | 補助成功 |
| 7.7 | [exec-call][tr-exec-instr]・[ADR0008][tr-adr-context] | [CallFunction][tr-call-function]・[ExecutionBoundary][tr-boundary] | [再帰・同期再入の共通上限][tr-t-call-exhaustion]・[trap/exhaustion/host例外後の復元][tr-t-call-reentry]・[正常/例外後の呼び直し][tr-t-call-host] | 補助成功 |
| 8.1 | [exec-instantiation][tr-exec-module]・[本仕様8.1][tr-req] | [各公開段階とExecutionBoundary][tr-boundary] | [Decode破損][tr-t-ref-malformed]・[Validate不成立][tr-t-element-invalid]・[Link不成立][tr-t-link-failure]・[trap][tr-t-table-bounds]・[exhaustion][tr-t-call-exhaustion]・[実装上限][tr-t-implementation-limit]・[未対応][tr-t-unsupported] | 補助成功 |
| 8.2 | [exec-instantiation][tr-exec-module]・[本仕様8.2][tr-req] | [ThrowIfFailed][tr-throw]・[InitializeElementSegments][tr-element-initialize] | [Invokeの関数・命令位置][tr-t-call-trap]・[Instantiateのsection9・要素位置][tr-t-element-failure]・[公開境界の要素診断][tr-t-section9-trap] | 補助成功 |
| 8.3 | [本仕様8.3][tr-req]・[ADR0012][tr-adr-diagnostic] | [AssertionJudge.Judge][tr-judge]・[GetTrapMessage][tr-boundary] | [段階・失敗・診断判定][tr-t-judge]・[7.2の全command結果][tr-run-7-2] | 補助成功・7.2成立、8.2で再確認 |
| 8.4 | [本仕様8.4][tr-req]・[ADR0007][tr-adr-host] | [Get/Set/TryGrow][tr-wasm-table]・[TableGrowの限定catch][tr-grow] | [公開ホスト誤用とcallbackのOOM原伝播][tr-t-host-exception]・[Setの範囲優先][tr-t-set-order]・[TryGrowの型検査][tr-t-grow-type] | 補助成功・OOM範囲確認 |
| 8.5 | [binary-elemsec][tr-bin-module]・[本仕様8.5][tr-req] | [InstructionSet][tr-def]・[section9読取][tr-element-decode]・[参照selectとv128分岐][tr-v128] | [対応済みopcode集合][tr-t-opcodes]・[全flags][tr-t-element-modes]・[参照typed select][tr-t-ref-control]・[v128のselect未対応][tr-t-v128] | 補助成功・7.2の対象未対応0 |
| 9.1 | [本仕様9.1][tr-req]・[ADR0003][tr-adr-core] | [SuiteExecutor.ExecuteAndSave][tr-suite] | [7.2の147入力・53,907command][tr-run-7-2] | 7.2記録確認・8.2待ち |
| 9.2 | [本仕様9.2][tr-req]・[ADR0003][tr-adr-core] | [SuiteExecutor][tr-suite]・[AssertionJudge][tr-judge] | [7.2の対象ケース監査][tr-audit-7-2] | 7.2成立確認・8.3待ち |
| 9.3 | [本仕様9.3][tr-req]・[ADR0003][tr-adr-core] | [BaselineComparer.CompareRun][tr-compare] | [passed欠落の回帰判定][tr-t-missing]・[7.2の固定比較][tr-compare-7-2] | 補助成功・8.2最終比較待ち |
| 9.4 | [本仕様9.4][tr-req]・[ADR0003][tr-adr-core] | [AssertionJudgeの未対応・依存判定][tr-judge] | [未実装診断の保持][tr-t-judge-unsupported]・[blockedの元原因][tr-t-judge-blocked]・[7.2ケース監査][tr-audit-7-2] | 補助成功・SIMD起因確認、8.3待ち |
| 9.5 | [本仕様9.5][tr-req]・[検証方針][tr-testing] | [既存TUnit3プロジェクト][tr-structure]・[55件照合][tr-check] | [runtimeのTRX][tr-runtime-trx]・[generatorのTRX][tr-generator-trx]・[runnerのTRX][tr-runner-trx] | 55件対応・補助4,569件成功 |
| 9.6 | [本仕様9.6][tr-req]・[ADR0003][tr-adr-core] | [BaselineStore.Save][tr-save] | [全分類・全内容をバイト列のまま保存][tr-t-baseline-save] | 保存機構成功・8.4操作待ち |

### 公開契約とOOMの確認範囲

`WasmTable.Get/Set`の範囲例外、`Set`の範囲検査優先、`TryGrow`の型検査・上限失敗時の状態保持は既存テストで成功した。
guestの`table.grow`は`TryGrow`呼び出しだけを`try`に含め、そこでの`OutOfMemoryException`だけを-1へ変換する。
`TryGrow`は新配列の割当、既存要素のコピー、追加領域の初期化を終えた後に`elements_`を置き換える。
初期領域の割当、公開ホスト操作、ホストcallbackへcatchを広げていないことをコードで確認した。
callbackが明示的に投げたOOMは、型と例外の同一性を保って伝播するテストで確認した。
実際の領域確保失敗によるOOMは再現していない。4.5と8.4のOOM変換・状態保持のうち、その経路はコード確認に限る。

対象opcodeとFC命令は対応済み集合のテスト、section9は全flagsの公開初期化、参照typed selectは公開Invokeで確認した。
v128のselectは既存の未対応分岐に残り、未対応fixtureもSIMDの診断と未確認範囲を検査する。
Stream、import/export、直接call、ホストcallback・同期再入、global・memory、数値・制御の既存回帰テストも今回のランタイム全件実行に含まれる。

### 今回の検証と後続受入

| 検証 | 結果 | 保存先 |
| --- | --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --warnaserror` | 終了0・警告0・エラー0 | [build.log][tr-build] |
| runtimeの`dotnet run -c Release --no-build` | 3,761件成功・失敗0・スキップ0 | [ログ][tr-runtime-log]・[TRX][tr-runtime-trx] |
| generatorの`dotnet run -c Release --no-build` | 106件成功・失敗0・スキップ0 | [ログ][tr-generator-log]・[TRX][tr-generator-trx] |
| runnerの`dotnet run -c Release --no-build` | 702件成功・失敗0・スキップ0 | [ログ][tr-runner-log]・[TRX][tr-runner-trx] |
| `dotnet csharpier check .` | 終了0・652ファイル確認 | [csharpier.log][tr-csharpier] |
| `git -c core.excludesFile= diff --check` | 終了0・空白エラーなし | [diff-check.log][tr-diff-check] |
| ビルド済みCLIの`--help` | 終了0 | [cli-help.log][tr-cli-help] |

テストは上記ビルド成功後にruntime→generator→runnerの順に実行し、各コマンドへ`--report-trx --results-directory`で本タスクの新規ディレクトリを指定した。
55行の番号・順序・非空セル・リンク先・規則ラベル・テスト宣言行とTRXの件数は、次の文書検査で照合する。
文書検査は追加前に行数0による終了1を確認し、対応表追加後に終了0を確認した。C#テストの新規追加・変更はない。

```powershell
python artifacts/tables-references/task-8.1-20261018T082940Z/check-coverage.py --output <新規の結果JSON>
dotnet csharpier check .
git -c core.excludesFile= diff --check
dotnet tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.dll --help
```

7.2の保存済みケース監査を再読し、全147入力・53,907commandの完了、passed27,482、runtime_unsupported1,079、blocked24,269、out_of_scope1,077、failed0、runner_error0を確認した。
同監査では、残るruntime_unsupportedの各ケースの報告機能はすべてSIMDであり、blockedも全件の起因をそのSIMD未対応ケースへ追跡している。
対象機能に由来するruntime_unsupportedとblockedは0件だった。
固定baseline比較の変化4,466件、追加・欠落・回帰・未比較・条件差・出典差・素材差各0件と、既知のmessageのみ33件も保存監査と照合した。
7.2の監査に記録された変更ソースのhashが現在も一致し、manifest・固定baseline・7.2結果のhashも一致した。
これらは7.2成立結果の再確認であり、8.2の新規全体実行と8.3の最終受入を代替しない。
8.4では、8.2・8.3で比較・受入を終えた同じ結果だけをbaseline-saveへ渡す。今回はbaselineを保存しておらず、Core 2.0全体の完成も宣言しない。

## 8.2 全公式commandの実行と固定baseline比較

要件9.1・9.3と設計のConformanceVerification・公式受入に従い、固定manifestから全147入力・53,907commandを新しい保存先へ実行した。素材・期待値・分類・比較元を保持し、同じ保存結果を固定baselineおよび7.2の保存結果と比較することをTask Briefとした。

成果物は[task-8.2-20261018T084643Z](../../../artifacts/tables-references/task-8.2-20261018T084643Z/)に保存した。

| 実行・比較 | 結果 | 保存結果 |
| --- | --- | --- |
| 固定manifestによる`run` | 終了0、処理・出力完了、入力異常・未処理0 | [run.json](../../../artifacts/tables-references/task-8.2-20261018T084643Z/run.json) |
| linear-memory受入済みbaselineとの`compare-run` | 終了0、成立・完了、差分4,466件 | [comparison.json](../../../artifacts/tables-references/task-8.2-20261018T084643Z/comparison.json) |
| 7.2の保存結果との`compare-run` | 終了0、成立・完了、差分0件 | [comparison-7.2.json](../../../artifacts/tables-references/task-8.2-20261018T084643Z/comparison-7.2.json) |

結果はpassed27,482、runtime_unsupported1,079、blocked24,269、out_of_scope1,077、failed0、runner_error0だった。両比較の条件差・出典差・入力素材差・追加・欠落・回帰・未比較はすべて0件だった。

[ケース監査](../../../artifacts/tables-references/task-8.2-20261018T084643Z/results-audit.json)では全commandの識別子・行・種別をmanifestと照合し、7.2の全ケース結果と一致することを確認した。固定baselineとの差分は、既知33件のmessageのみの差、runtime_unsupportedからpassedへの378件、blockedからpassedへの4,054件、`simd/simd_const.wast#573`の停止位置変更1件である。既知33件は1.1のcaseID一覧および6.1の保存結果と照合し、message以外の不変を確認した。最後の1件はelementのsection9から`v128.const`のsection10へ進んだものだった。差分4,466件は7.2の比較結果とケース単位で一致し、7.2で成立していたケースの回帰はない。

5,821素材と147入力のhashを再計算した。[事前確認](../../../artifacts/tables-references/task-8.2-20261018T084643Z/preflight.json)に現在のsrc・tests・tools配下659ファイルとCLIのhashを保存し、7.2の変更13ファイルとの一致および公式実行前後の不変を確認した。manifestのSHA256は`c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e`、固定baselineは`70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1`のままである。

今回の比較済みrunのSHA256は`9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496`である。[コマンド記録](../../../artifacts/tables-references/task-8.2-20261018T084643Z/commands.json)と各ログに引数・終了値を保存し、ビルド済みCLIの`--help`、ケース監査、`git diff --check`も終了0だった。製品・テストの変更はなく、8.1で完了した3プロジェクトのテストは重複実行していない。

8.3ではこの同じrunを使って対象規則・ケースと残る未対応・前提不成立の起因を最終照合する。8.4のbaseline-saveは未実行であり、本節は対象ケースの最終受入やCore 2.0全体の完成を宣言しない。

## 8.3 対象ケースと残る未対応の起因の照合

要件9.2・9.4・9.5と設計のConformanceVerification・公式受入に従い、8.2の同じ比較済みrun（SHA256 `9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496`）を受入対象とした。全53,907ケースが7.2の保存結果と一致し、固定素材のケース識別子・行・種別とも一致することを[今回の監査](../../../artifacts/tables-references/task-8.3-20261018T090100Z/results-audit-initial.json)で確認した。

| 重点確認した公式入力 | passed | out_of_scope |
| --- | ---: | ---: |
| ref_null・ref_func・ref_is_null・select・global | 291 | 3 |
| table・table_get/set/size/grow/fill/copy/init | 2,697 | 6 |
| elem | 95 | 0 |
| call_indirect・func_ptrs | 195 | 11 |
| bulk・linking・start | 268 | 1 |

上記19入力の前提が揃う3,546ケースはすべてpassedだった。複数table、一括操作、間接call、初期化失敗を含み、Instantiate失敗を期待する20ケースも期待段階でpassedとなった。対象外21ケースは既存の分類を保持し、passedへ合算していない。重点入力だけで範囲を限定せず、全147入力の残るruntime_unsupported1,079件とblocked24,269件も照合した。

未対応は各ケースの公開診断に報告されたSIMD命令であり、blockedは全件の起因ケースがそのSIMD未対応へ到達した。対象機能に由来するruntime_unsupportedとblockedは0件だった。現在runから再計算した全ケースの機能・起因が[7.2のケース別一覧][tr-audit-7-2]と一致することを確認し、一覧のhash、各ケースの出典、機能別件数を今回の監査へ結び付けた。フォルダ名だけでは判断していない。out_of_scope1,077件は別分類のまま保持する。

8.1の[55基準対応表](#55受入基準の対応)を再利用し、[今回の対応検査](../../../artifacts/tables-references/task-8.3-20261018T090100Z/coverage-initial.json)で55行の規則・実装・リンクと60テストメソッド、既存TRXのruntime3,761件・generator106件・runner702件の全成功を再照合した。製品・テスト・CLIのhashも8.2時点と一致する。公式runやテストは重複実行しておらず、実際の領域確保失敗によるOOMの未再現範囲は8.1の記載どおりである。

[監査スクリプト](../../../artifacts/tables-references/task-8.3-20261018T090100Z/audit-acceptance.py)と[引数・終了値](../../../artifacts/tables-references/task-8.3-20261018T090100Z/commands-initial.json)を保存した。55基準検査とケース監査はいずれも終了0だった。8.1時点の表と記述は履歴として保持し、最新の判定は9.1・9.3が8.2で成立、9.2・9.4・9.5が本節までで成立である。9.6は8.4の明示baseline保存待ちであり、本タスクでは保存していない。残るSIMD待ちを保持した機能受入であり、Core 2.0全体の完成は宣言しない。

## 8.4 比較済み結果の明示baseline保存

要件9.6と設計のConformanceVerification・公式受入に従い、8.2で比較し8.3で受け入れた同じrunだけを既存CLIのbaseline-saveへ渡した。新しい保存先が存在しないことを確認して実行し、終了0だった。

後続機能では、[今回のrun-baseline.json](../../../artifacts/tables-references/task-8.4-20261018T090714Z/run-baseline.json)と[対応manifest](../../../artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json)を組にして使用する。SHA256はbaselineが`9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496`、manifestが`c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e`である。

[今回の照合](../../../artifacts/tables-references/task-8.4-20261018T090714Z/checks.json)で入力とbaselineのバイト列が完全一致し、全147入力・53,907command、分類・素材・出典・実行条件、処理・出力の完了を保持したことを確認した。内包するcorpus全体と参照pathも対応manifestと一致した。入力run・manifest・旧linear-memoryのbaselineは保存前後で不変であり、旧baselineは上書きしていない。[引数・終了値](../../../artifacts/tables-references/task-8.4-20261018T090714Z/commands.json)と[検証記録](../../../artifacts/tables-references/task-8.4-20261018T090714Z/verification.md)へ保存結果をまとめた。

最新の判定は9.1・9.3が8.2、9.2・9.4・9.5が8.1・8.3、9.6が本節で成立であり、55受入基準は本機能の範囲で最終受入済みである。8.1の55行対応表と8.2・8.3の待ち状態は各時点の履歴として保持する。残るSIMD待ちのruntime_unsupported1,079件・blocked24,269件とout_of_scope1,077件を保持し、Core 2.0全体の完成は宣言しない。実際の領域確保失敗によるOOMの未再現範囲も8.1の記載どおりである。

[tr-bin-instr]: ../../../thirdParties/WebAssembly-spec/document/core/binary/instructions.rst
[tr-bin-module]: ../../../thirdParties/WebAssembly-spec/document/core/binary/modules.rst
[tr-valid-instr]: ../../../thirdParties/WebAssembly-spec/document/core/valid/instructions.rst
[tr-valid-module]: ../../../thirdParties/WebAssembly-spec/document/core/valid/modules.rst
[tr-exec-instr]: ../../../thirdParties/WebAssembly-spec/document/core/exec/instructions.rst
[tr-exec-module]: ../../../thirdParties/WebAssembly-spec/document/core/exec/modules.rst
[tr-spec-eval]: ../../../thirdParties/WebAssembly-spec/interpreter/exec/eval.ml#L201
[tr-req]: requirements.md
[tr-testing]: design.md#検証方針testing-strategy
[tr-structure]: ../../../.kiro/steering/structure.md
[tr-adr-host]: ../../../docs/adr/0007-propagate-host-exceptions.md
[tr-adr-context]: ../../../docs/adr/0008-instance-options-and-execution-context.md
[tr-adr-instance]: ../../../docs/adr/0010-host-function-instance-context.md
[tr-adr-retain]: ../../../docs/adr/0011-retain-references-after-start-failure.md
[tr-adr-diagnostic]: ../../../docs/adr/0012-reference-diagnostic-compatibility.md
[tr-adr-core]: ../../../docs/adr/0003-core2-fixed-conformance-profile.md
[tr-def]: ../../../src/WasmSharp/Instructions/InstructionSet.cs
[tr-decode]: ../../../src/WasmSharp/Modules/ExpressionDecoder.cs#L23
[tr-element-decode]: ../../../src/WasmSharp/Modules/ModuleDecoder.cs#L298
[tr-validator]: ../../../src/WasmSharp/Modules/FunctionValidator.cs
[tr-context]: ../../../src/WasmSharp/Modules/ReferenceValidationContext.cs#L53
[tr-element-validate]: ../../../src/WasmSharp/Modules/ModuleValidator.cs#L45
[tr-constant]: ../../../src/WasmSharp/Modules/ConstantInitializer.cs#L20
[tr-module-validate]: ../../../src/WasmSharp/WasmModule.cs#L258
[tr-v128]: ../../../src/WasmSharp/Modules/FunctionValidator.cs#L682
[tr-push]: ../../../src/WasmSharp/Execution/Interpreter.cs#L246
[tr-ref]: ../../../src/WasmSharp/Execution/Interpreter.Reference.cs#L11
[tr-is-null]: ../../../src/WasmSharp/Execution/Interpreter.Reference.cs#L27
[tr-interpreter]: ../../../src/WasmSharp/Execution/Interpreter.cs
[tr-select]: ../../../src/WasmSharp/Execution/Interpreter.Control.cs#L85
[tr-table]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L123
[tr-wasm-table]: ../../../src/WasmSharp/WasmTable.cs
[tr-size]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L169
[tr-grow]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L185
[tr-trygrow]: ../../../src/WasmSharp/WasmTable.cs#L125
[tr-copy]: ../../../src/WasmSharp/WasmTable.cs#L219
[tr-table-copy]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L13
[tr-fill]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L44
[tr-init]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L71
[tr-element]: ../../../src/WasmSharp/Execution/ElementInstance.cs#L27
[tr-drop]: ../../../src/WasmSharp/Execution/Interpreter.Table.cs#L108
[tr-element-drop]: ../../../src/WasmSharp/Execution/ElementInstance.cs#L41
[tr-instance]: ../../../src/WasmSharp/WasmInstance.cs#L78
[tr-instantiate]: ../../../src/WasmSharp/Modules/ModuleInstantiator.cs#L32
[tr-element-initialize]: ../../../src/WasmSharp/Modules/ModuleInstantiator.cs#L98
[tr-call]: ../../../src/WasmSharp/Execution/Interpreter.Call.cs#L15
[tr-call-function]: ../../../src/WasmSharp/Execution/Interpreter.Call.cs#L88
[tr-boundary]: ../../../src/WasmSharp/Execution/ExecutionBoundary.cs
[tr-throw]: ../../../src/WasmSharp/Execution/ExecutionBoundary.cs#L113
[tr-judge]: ../../../tools/WasmSharp.TestSuiteRunner/Execution/AssertionJudge.cs#L17
[tr-suite]: ../../../tools/WasmSharp.TestSuiteRunner/Execution/SuiteExecutor.cs#L119
[tr-compare]: ../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineComparer.cs#L18
[tr-save]: ../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineStore.cs#L16
[tr-t-ref-decode]: ../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs#L12
[tr-t-call-decode]: ../../../tests/WasmSharp.Tests/WasmModule_DecodeIndirectCallTests.cs#L11
[tr-t-bulk-decode]: ../../../tests/WasmSharp.Tests/WasmModule_DecodeBulkTableTests.cs#L12
[tr-t-flags]: ../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadElementTests.cs#L149
[tr-t-element-modes]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateElementIntegrationTests.cs#L17
[tr-t-element-syntax]: ../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadElementTests.cs#L251
[tr-t-element-eof]: ../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadElementTests.cs#L297
[tr-t-ref-malformed]: ../../../tests/WasmSharp.Tests/WasmModule_DecodeReferenceTests.cs#L15
[tr-t-element-static]: ../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadElementTests.cs#L192
[tr-t-element-invalid]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateElementIntegrationTests.cs#L17
[tr-t-unsupported]: ../../../tests/WasmSharp.Tests/WasmModule_DecodeTests.cs#L462
[tr-t-expression-unsupported]: ../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs#L505
[tr-t-v128]: ../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateSelectTests.cs#L11
[tr-t-bulk-invalid]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateBulkTableTests.cs#L25
[tr-t-call-invalid]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateIndirectCallTests.cs#L17
[tr-t-ref-declaration]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateReferenceTests.cs#L15
[tr-t-declarations]: ../../../tests/WasmSharp.Tests/Modules/ReferenceValidationContext_CreateTests.cs#L118
[tr-t-constant]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateReferenceTests.cs#L145
[tr-t-element-global]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateElementIntegrationTests.cs#L58
[tr-t-import-global]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateReferenceIntegrationTests.cs#L10
[tr-t-ref-unreachable]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateReferenceTests.cs#L115
[tr-t-validation-state]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateTests.cs#L36
[tr-t-ref-null]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeReferenceTests.cs#L10
[tr-t-ref-identity]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeReferenceTests.cs#L49
[tr-t-ref-control]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeReferenceTests.cs#L108
[tr-t-select-invalid]: ../../../tests/WasmSharp.Tests/WasmModule_ValidateReferenceTests.cs#L73
[tr-t-table-getset]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeTableTests.cs#L13
[tr-t-table-bounds]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeTableTests.cs#L165
[tr-t-table-shared]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeTableTests.cs#L57
[tr-t-host-grow]: ../../../tests/WasmSharp.Tests/WasmTable_TryGrowTests.cs#L10
[tr-t-table-limit]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeTableTests.cs#L118
[tr-t-host-limit]: ../../../tests/WasmSharp.Tests/WasmTable_TryGrowTests.cs#L158
[tr-t-grow-zero]: ../../../tests/WasmSharp.Tests/Execution/Interpreter_TableGrowTests.cs#L57
[tr-t-copy-fill]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkTableTests.cs#L150
[tr-t-bulk-modes]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkTableTests.cs#L17
[tr-t-passive]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkTableTests.cs#L109
[tr-t-bulk-bounds]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkTableTests.cs#L252
[tr-t-global-function]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateReferenceIntegrationTests.cs#L47
[tr-t-element-reference]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateElementIntegrationTests.cs#L74
[tr-t-element-failure]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateElementIntegrationTests.cs#L127
[tr-t-retained-function]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateIndirectCallTests.cs#L11
[tr-t-element-data-start]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateElementIntegrationTests.cs#L262
[tr-t-start-failure]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateStartTests.cs#L169
[tr-t-link-failure]: ../../../tests/WasmSharp.Tests/WasmModule_InstantiateElementIntegrationTests.cs#L410
[tr-t-call-values]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeIndirectCallTests.cs#L9
[tr-t-call-trap]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeIndirectCallTests.cs#L96
[tr-t-call-host]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeIndirectCallTests.cs#L161
[tr-t-call-exhaustion]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeIndirectCallTests.cs#L210
[tr-t-call-reentry]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeIndirectCallTests.cs#L267
[tr-t-implementation-limit]: ../../../tests/WasmSharp.Tests/WasmTable_ConstructorTests.cs#L83
[tr-t-section9-trap]: ../../../tests/WasmSharp.Tests/Execution/ExecutionBoundary_ThrowIfFailedTests.cs#L274
[tr-t-judge]: ../../../tests/WasmSharp.TestSuiteRunner.Tests/Execution/AssertionJudge_JudgeTests.cs#L23
[tr-t-host-exception]: ../../../tests/WasmSharp.Tests/WasmFunction_InvokeTableTests.cs#L216
[tr-t-set-order]: ../../../tests/WasmSharp.Tests/WasmTable_SetTests.cs#L105
[tr-t-grow-type]: ../../../tests/WasmSharp.Tests/WasmTable_TryGrowTests.cs#L113
[tr-t-opcodes]: ../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs#L13
[tr-t-missing]: ../../../tests/WasmSharp.TestSuiteRunner.Tests/Baselines/BaselineComparer_CompareRunTests.cs#L168
[tr-t-judge-unsupported]: ../../../tests/WasmSharp.TestSuiteRunner.Tests/Execution/AssertionJudge_JudgeTests.cs#L296
[tr-t-judge-blocked]: ../../../tests/WasmSharp.TestSuiteRunner.Tests/Execution/AssertionJudge_JudgeTests.cs#L330
[tr-t-baseline-save]: ../../../tests/WasmSharp.TestSuiteRunner.Tests/Baselines/BaselineStore_SaveTests.cs#L11
[tr-run-7-2]: ../../../artifacts/tables-references/task-7.2-indirect-call-20261018T080623Z/run.json
[tr-audit-7-2]: ../../../artifacts/tables-references/task-7.2-indirect-call-20261018T080623Z/results-audit-final.json
[tr-compare-7-2]: ../../../artifacts/tables-references/task-7.2-indirect-call-20261018T080623Z/comparison.json
[tr-check]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/check-coverage.py
[tr-coverage]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/coverage-final.json
[tr-artifacts]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/
[tr-build]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/build.log
[tr-runtime-log]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/runtime-tests.log
[tr-runtime-trx]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/runtime/WasmSharp.Tests_net10.0_x64.trx
[tr-generator-log]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/generator-tests.log
[tr-generator-trx]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/generator/WasmSharp.Generators.Tests_net10.0_x64.trx
[tr-runner-log]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/runner-tests.log
[tr-runner-trx]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/runner/WasmSharp.TestSuiteRunner.Tests_net10.0_x64.trx
[tr-csharpier]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/csharpier.log
[tr-diff-check]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/diff-check.log
[tr-cli-help]: ../../../artifacts/tables-references/task-8.1-20261018T082940Z/cli-help.log
