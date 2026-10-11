# 数値・制御の検証記録

## タスク1.1:固定素材と比較元の保持

2026-10-11に実装前の環境を確認した。確認範囲は要件10.1・10.5の準備と、設計の「責務境界」「公式受入のBatch契約」である。ランタイム・生成器・ランナーのコードとテストは変更していない。全TUnitと固定公式スイートの実行、数値・制御の受入判定は後続タスクで行う。

タスクの受入条件・完了定義・設計制約・確認方法は、作業前に[Task Brief](../../../artifacts/numeric-control/task-1.1-20261011T163926/task-brief.txt)へ保存した。以下の出力先は作成前に存在しないことを確認し、`git check-ignore -v artifacts/numeric-control/task-1.1-20261011T163926/`が終了0、`.gitignore`の66行目の`artifacts/`に一致することを確認した。

`artifacts/numeric-control/task-1.1-20261011T163926/`

### 環境と実行前提

| 対象 | 確認結果 |
| --- | --- |
| .NET | SDK10.0.401、Microsoft.NETCore.App10.0.12、Windows x64。`dotnet --info`は終了0。 |
| 固定spec | `thirdParties/WebAssembly-spec`のHEADは`05ca4182176763112561ae20153975c12bd689e4`。 |
| 固定WABT | `thirdParties/wabt`のHEADは`03a00a1334e6121fb0cce4fccbd6bb109b68acaa`。 |
| TUnit | 既存3プロジェクトは`net10.0`・`OutputType=Exe`・TUnit1.66.16。Releaseビルド後の`--help`はすべて終了0。 |
| テスト素材 | ランタイムの固定spec命令付録、生成器の既存契約ソース、ランナーテストのConverterFixtureを含むビルドが成立。 |
| 依存復元 | 標準ReleaseビルドでNuGet復元は全プロジェクト最新。不足依存の取得やGit設定の変更は不要だった。 |
| ランナー | Releaseのapphostで`--help`と`baseline-save`が終了0。生成済み素材の実行にはWABTの再ビルドを必要としない。 |

実行手順は`README.md`、`.github/workflows/unit-tests.yml`、`tools/WasmSharp.TestSuiteRunner/README.md`、`thirdParties/README.md`に合わせた。コマンドはリポジトリルートから実行し、このタスクでは`require_escalated`で起動した。

環境情報は[dotnet-info.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/dotnet-info.log)、固定commitは[submodule-status.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/submodule-status.log)、無視設定は[check-ignore.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/check-ignore.log)に保存した。

### 固定素材・プロファイルの照合

| 対象 | パス・SHA-256・結果 |
| --- | --- |
| manifest | `artifacts/test-suite-runner-acceptance-20261001/portable/corpus/manifest.json`。SHA-256:`5ce710f306e91f380ff45797aafeb7a0fbfd66882ce45a6d4bc63f5c27de9b6e`。 |
| 固定profile | manifestの`profile`と`tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json`は全項目が一致。`core2`、固定spec/WABT commit、変換条件、論理引数、入力集合・hash、21feature状態を維持。 |
| baselineの素材識別 | 元baselineの`corpus`とmanifest全体が一致。両方の`processing_complete`・`output_complete`は`true`。 |
| 公式入力 | 固定specの全147WAST（SIMD57件）について、集合とSHA-256を固定profileと照合。不足・hash不一致0。構文解析は行わずバイト列を照合した。 |
| 生成物 | JSON147件・`.wasm`4,597件・`.wat`1,077件、合計5,821件の存在とSHA-256をmanifestと照合。不足・hash不一致・重複パス0。 |
| 変換器 | `artifacts/wabt-core2/Release/wast2json.exe`のSHA-256は`b0e1d0316a265f659566a0e3b6f1a00fea9e315e3d25f2c3e0853e414d0be245`で、manifestの生成時記録と一致。素材不足がないため再生成しなかった。 |

21featureは既存固定profileどおり、ON7件（`mutable-globals`、`saturating-float-to-int`、`sign-extension`、`simd`、`multi-value`、`bulk-memory`、`reference-types`）、OFF14件（`exceptions`、`threads`、`function-references`、`tail-call`、`annotations`、`code-metadata`、`gc`、`memory64`、`multi-memory`、`extended-const`、`relaxed-simd`、`custom-page-sizes`、`compact-imports`、`wide-arithmetic`）である。feature変更の引数や入力除外は使用していない。

照合コマンドは`python -X utf8 artifacts/numeric-control/task-1.1-20261011T163926/audit-fixed-corpus.py`。終了0、`issue_count=0`。一時的な確認スクリプトと全入力・生成物の再計算hashは、専用出力先の[audit-fixed-corpus.py](../../../artifacts/numeric-control/task-1.1-20261011T163926/audit-fixed-corpus.py)、[fixed-corpus-audit.json](../../../artifacts/numeric-control/task-1.1-20261011T163926/fixed-corpus-audit.json)、[fixed-corpus-audit.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/fixed-corpus-audit.log)に保持した。

### 変更前baselineの保存

| 用途 | パス |
| --- | --- |
| 保存元 | `artifacts/test-suite-runner-acceptance-20261001/run-baseline.json` |
| 後続タスクの比較元 | `artifacts/numeric-control/task-1.1-20261011T163926/baseline-before.json` |

両ファイルのSHA-256は`0f47b1fc0b648b8e539297a60a3ef5a36c7d78c79ef3f8550ef86b10a5814285`で、設計に記載された値と一致する。元baselineは上書きしていない。保存後にも両ファイルのhashを再計算し、[baseline-hashes.json](../../../artifacts/numeric-control/task-1.1-20261011T163926/baseline-hashes.json)へ記録した。

既存baselineには全147入力・53,907commandが記録されている。内訳はpassed2,491、failed0、runtime_unsupported2,987、blocked47,352、out_of_scope1,077、runner_error0。この数値は保存した比較元の内容であり、今回の全体実行結果や数値・制御の受入結果ではない。

```powershell
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe baseline-save --input artifacts/test-suite-runner-acceptance-20261001/run-baseline.json --output artifacts/numeric-control/task-1.1-20261011T163926/baseline-before.json
```

この操作は終了0。[baseline-save.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/baseline-save.log)と`baseline-save.exit`に保存した。後続の完了単位では、同じmanifestから新しい`artifacts/numeric-control/<run-id>/run.json`へ全体実行し、記録完了・終了0を確認してから、上記の`baseline-before.json`を指定して`comparison.json`を作成する。最後の受入までこの比較元を更新しない。

### 実行コマンドと終了値

各ログと`.exit`は専用出力先に保存した。TUnitの3行は起動前提の確認であり、テストケースの実行ではない。

| コマンド | 終了値 | ログ・結果 |
| --- | --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --warnaserror` | 0 | [build-release.log](../../../artifacts/numeric-control/task-1.1-20261011T163926/build-release.log)。警告0・エラー0。 |
| `./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe --help` | 0 | `runner-help.log`・`runner-help.exit`。 |
| `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --help` | 0 | `WasmSharp.Tests-help.log`・`WasmSharp.Tests-help.exit`。 |
| `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --help` | 0 | `WasmSharp.Generators.Tests-help.log`・`WasmSharp.Generators.Tests-help.exit`。 |
| `dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --help` | 0 | `WasmSharp.TestSuiteRunner.Tests-help.log`・`WasmSharp.TestSuiteRunner.Tests-help.exit`。 |
| `python -X utf8 artifacts/numeric-control/task-1.1-20261011T163926/audit-fixed-corpus.py` | 0 | `fixed-corpus-audit.log`・`fixed-corpus-audit.exit`。不一致0。 |
| 上記の`baseline-save` | 0 | `baseline-save.log`・`baseline-save.exit`。保存元と保存先のhash一致。 |

タスク1.1の準備条件は満たした。要件10.1・10.5が要求する全体実行と回帰比較の判定は、保存した比較元を使う後続タスクの結果として追記する。

### 独立レビューと完了確認

履歴を引き継がない別エージェントの`kiro-review`は`APPROVED`だった。3つのTUnitを直列実行し、WasmSharp.Tests1,114件、WasmSharp.Generators.Tests37件、WasmSharp.TestSuiteRunner.Tests701件が成功した。各終了0、失敗・スキップ0。固定147入力・5,821生成物のhashも独立に再計算し、不一致0だった。レビューのログと結果は`artifacts/numeric-control/task-1.1-review-20261011T074751Z/`に保存した。

親エージェントも現在のビルド・テストログと終了値、差分、保存元と保存先の再計算hashを確認した。`kiro-verify-completion`の判定は`TASK: VERIFIED`。この判定はタスク1.1の準備範囲である。

## タスク1.2:バイナリfixtureの拡充

`HostLinkingModuleBinary`に`Signed`、`LabelVector`、`TypeVector`を追加し、既存`Types`から型列の符号化を再利用した。入れ子制御・複合即値・不正な生バイト列は既存の組立方法で明示的に構築する。変更は同fixtureと関連テストの5ファイルで、ランタイムの振る舞いは変更していない。要件1.1・1.3・3.9・7.9に対する入力構築の準備範囲である。

- 実装者:`READY_FOR_REVIEW`。REDは符号付き整数11件失敗、ベクトル5件失敗を終了2で確認した。
- 最終Releaseビルド:終了0、警告・エラー0。fixtureのfocused32件成功、ランタイム全1,139件成功、失敗・スキップ0。変更5ファイルのCSharpierと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現在の3suiteは1,139件・37件・701件成功、各終了0、失敗・スキップ0。仕様・差分・RED・境界・レビュー前後のsource hash一致を確認した。
- 実装ログ:`artifacts/numeric-control/task-1.2-20261011-fixture-6f329b/`。レビュー記録:`artifacts/numeric-control/task-1.2-review-20261011-a83e/validation.md`。

親エージェントが差分、最終ビルド・テストの結果と終了値を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公式全体実行と数値・制御の機能受入は後続タスクで行う。

## タスク2.1:式読取りの責務分離

関数本体と定数式の読取りを`ExpressionDecoder.Read`へ抽出した。`ModuleDecoder`はsection処理とBounded/Diagnosticの入口を保持し、式読取りを委譲する。sectionと式が共有するUnsupported診断の生成は、既存readerの診断契約へ移して逆向き依存と重複を避けた。変更はModuleDecoder・ModuleBinaryReader・ExpressionDecoderと直接契約テストの4ファイル。要件1.1・1.4・1.6・9.1・9.6・9.7の既存振る舞いを保つ範囲である。

- 実装者:`READY_FOR_REVIEW`。非behavioralの抽出として、直接16件を先に作成し、抽出前後の同じfocused266件成功を確認した。仮の委譲入口は除去した。
- 最終Releaseビルド:終了0、警告・エラー0。ランタイム全1,155件成功、失敗・スキップ0。CSharpier4ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのReleaseビルドと3suiteを確認し、1,155件・37件・701件成功、各終了0、失敗・スキップ0。移動前後の処理本体の等価性と境界を確認した。
- 実装記録:`artifacts/numeric-control/task-2.1-20261011T180500/verification.md`。レビューのログ・終了値・処理比較・source hash:`artifacts/numeric-control/task-2.1-review-20261011T191500/`。

親エージェントも差分、現在のビルド・3suiteのログと終了値を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。固定公式全体の実行と回帰比較はタスク2.3で行う。

## タスク2.2:関数検証の責務分離

1関数の型検査と線形化を`FunctionValidator.Validate`へ抽出した。解決済みの関数型・global型を利用し、入力配列やmoduleの検証状態を変更しない。module全体の検査・定数式の適格性は`ModuleValidator`に残し、共有する既存診断2メソッドだけを`ValidationDiagnostics`へ移した。変更はこの3ファイルと直接境界・後続失敗のテスト2ファイル。要件1.5・2.6・3.8・8.3・9.1の既存振る舞いを維持する範囲である。

- 実装者:`READY_FOR_REVIEW`。非behavioralの抽出として、追加したcharacterizationを含む同じfocused123件が抽出前後で成功。一時入口は除去済み。
- 最終Releaseビルド:終了0、警告・エラー0。ランタイム全1,160件成功、失敗・スキップ0。CSharpier5ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのReleaseビルドと3suiteを確認し、1,160件・37件・701件成功、各終了0、失敗・スキップ0。移動前後の本体・診断・検査順と全体成功時だけの状態確定を確認した。
- focusedの初期化式filterはファイル名を指定していたため選択されていなかった。初期化式は抽出後と独立レビューのランタイム全suiteで成功を確認した。実装者の記録も訂正した。
- 実装記録:`artifacts/numeric-control/task-2.2-20261011T172542/verification.md`。独立レビュー:`artifacts/numeric-control/task-2.2-review-20261011T180100/review.md`。各出力先にコマンド・ログ・終了値を保持した。

親エージェントが差分、現在のビルド・3suiteのログと終了値、レビューの非阻害指摘への記録訂正を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。固定公式全体の実行と回帰比較はタスク2.3で行う。

## タスク2.3:責務分離後の公開経路と公式結果

既存の公開APIテストと接続コードで、Decode→Validate→Instantiate→Invoke、Decode・Validateのstart/callback非実行、全体検証成功後だけの状態確定を確認した。既存テストがこの範囲を覆うため、コード・テストの追加変更はない。要件1.1・1.5・9.6・9.7・10.1・10.5・10.7の責務分離後の統合確認である。

- 実装者:`READY_FOR_REVIEW`。Releaseビルド警告・エラー0、3suiteは1,160件・37件・701件成功、失敗・スキップ0、各終了0。ReleaseのCLI`--help`も終了0。
- 公式出力:`artifacts/numeric-control/task-2.3-20261011T174124/run.json`。固定147入力・53,907commandを処理し、`processing_complete`と`output_complete`が`true`、終了0。
- 結果:passed2,491、failed0、runtime_unsupported2,987、blocked47,352、out_of_scope1,077、runner_error0。
- 記録完了・終了0の確認後、タスク1.1で保存した比較元へ`compare-run`を実行。`artifacts/numeric-control/task-2.3-20261011T174124/comparison.json`は比較成立・完了、終了0。条件差・出典差・素材差・変化・追加・欠落・回帰・未比較、現在のfailed・runner_errorはすべて0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのビルドと3suiteを独立実行し、同じ成功件数と各終了0を確認。全53,907件の保存記録一致、以前passedの2,491件の保持、公式run完了後の比較順序を直接照合した。
- 実装記録:`artifacts/numeric-control/task-2.3-20261011T174124/verification.md`。独立レビュー:`artifacts/numeric-control/task-2.3-review-20261011T174930/review.md`。各出力先にコマンド・ログ・終了値・集計監査を保持した。

親エージェントも現在のビルド・3suiteと公式実行・比較の出力と終了値、保存JSONの完了・集計、独立監査を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。比較元は更新していない。承認は責務分離後の統合範囲に限り、後続の数値・制御追加と最終受入は未実施である。

## タスク3.1:s33と複合即値の入力契約

既存LEB128処理を再利用する`ReadS33`、空型・単一値型・型添字を区別する`BlockType`、即値別factoryで構築する`DecodedInstruction`を追加した。ラベル列とselect型列は入力をコピーし、不変配列として保持する。既存の式読取りとfixtureの生成をfactoryへ揃え、生成器fixtureも本体の値・型契約へ移行した。変更は本体4ファイル、生成器fixture3ファイル、ランタイムテスト5ファイル。要件1.2・1.3・1.4の入力契約を整える範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後に新規39件を実行し、17件失敗・22件成功、終了2。ONで39件成功し、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。入力focused147件、生成器37件、ランタイム全1,199件成功、失敗・スキップ0。CSharpier11ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのビルドと3suiteを確認し、1,199件・37件・701件成功、各終了0、失敗・スキップ0。s33の境界・診断、タグの区別、配列の不変所有、不要即値の統一と既存生成APIの移行を確認した。
- RED終了値は記録の転記ミスを実ファイルの2へ訂正した。コード変更はない。
- 実装記録:`artifacts/numeric-control/task-3.1-20261011T1900/verification.md`。独立レビュー:`artifacts/numeric-control/task-3.1-review-20261011T2011/review.md`。各出力先にコマンド・ログ・終了値を保持した。

親エージェントが差分、現在のビルド・3suiteとREDのログ・終了値、記録訂正を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。新opcode・入れ子構文・Validateの接続と公式全体の次回比較は後続タスクで行う。

## タスク3.2:関数が所有する静的分岐情報

設計の`BranchTarget`、`FunctionCode.BranchTargets`、`Instruction.TargetCount`を追加した。候補の後に既定を置き、既定を除く候補数0を表現できる。末尾の省略可能な引数により既存構築を維持し、共有する静的コードに実行状態を追加していない。変更は本体3ファイルと契約・2instance共有のテスト3ファイル。要件3.4・7.7・7.8・7.9の静的表現の範囲である。

- 実装者:`READY_FOR_REVIEW`。先に契約テストを追加し、形状だけの足場でビルド成功後に6件中3件失敗・終了2を確認。格納実装後のfocused92件が成功した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,206件・37件・701件成功、失敗・スキップ0、各終了0。CSharpier6ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのビルドと同じ3suiteの成功、RED/GREEN、指定データ契約と既存構築・結果の維持を確認した。
- 実装記録:`artifacts/numeric-control/task-3.2-20261011T182008/verification.md`。独立レビュー:`artifacts/numeric-control/task-3.2-review-20261011T185500/review.md`。各出力先にコマンド・ログ・終了値を保持した。

親エージェントが差分、現在のビルド・3suiteとRED/GREENの出力・終了値を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。分岐操作・handler・制御のDecode/Validate接続と公式全体の確認は後続タスクで行う。

## タスク3.3:数値型効果の宣言と生成器契約

既存3引数・7引数属性を維持し、10引数の数値signatureを生成descriptorへ投影した。Numeric両規則とsignatureの整合、入力個数1/2、入出力のスカラー4型を検査し、既存の不完全宣言・opcode重複・handler診断を保持する。複合即値・制御・selectの共通enumと、本体契約を埋め込む生成器fixtureも整えた。変更は本体・生成器6ファイルと専用fixture・テスト・projectの3ファイル。要件1.2・1.3・2.1・2.2・4.6・4.7・9.2の宣言契約の範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後に27件中12件失敗・15件成功、終了2。ONで27件成功し、一時flagを除去した。途中で見つかったfixtureの必須indexと実行契約の取込み不足は修正済み。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,206件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier8ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。現ソースのビルドと同じ3suiteの成功、RED/GREEN、直接dispatchと失敗結果の伝播、診断、生成器の依存関係とfixtureの契約を確認した。
- 実装記録:`artifacts/numeric-control/task-3.3-20261011T183600/verification.md`。独立レビュー:`artifacts/numeric-control/task-3.3-review-20261011-independent-8cf463/review.md`。各出力先にコマンド・ログ・終了値を保持した。

親エージェントが差分、新規signature、現在のビルド・3suiteとRED/GREENの出力・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。対応済みopcodeへの切替、handler・FunctionValidator・公開経路と公式全体の確認は後続タスクで行う。

## タスク4.1:整数算術と比較handler

`Interpreter.Integer.cs`にi32/i64のadd・sub・mul、eqzとsigned/unsigned比較の28handlerを追加した。右operandから取り出し、算術はuncheckedで対象幅の剰余、unsigned比較は同幅のビット解釈、比較結果はi32の0/1とする。直接テスト28クラスとcontext準備だけを共有するfixtureを追加した。変更はこの30ファイル。要件4.1・4.7の実行handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後、28クラスの93件すべて失敗、終了2。ONで同じ93件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,299件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier30ファイルと空白確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。新規30ファイルの全文、仕様・設計、幅境界・左右・符号解釈・結果型・下位operand保持のテスト、RED/GREENを確認し、独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-4.1-20261011T184000/verification.md`。独立レビュー:`artifacts/numeric-control/task-4.1-review-20261011T190000/review.md`。各出力先に変更ファイル一覧・コマンド・ログ・終了値を保持した。

親エージェントがhandler全体、代表テスト・fixture、現在のビルド・3suiteとRED/GREENの出力・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。数値opcodeの宣言・型検証・公開実行への接続は7.1、公式全体の次回確認は7.2で行う。

## タスク4.2:整数除算・剰余と位置付きtrap

既存整数partialにsigned/unsignedのdiv/remを両幅8handler追加した。0除数とsigned除算MIN/-1を演算前に判定し、Reasonと関数index・バイト位置を持つtrapを返す。同じ入力のsigned剰余は0で正常終了する。共有するtrap構築はprivateの`IntegerTrap`へ閉じた。変更は本体1ファイルと直接テスト8クラス。要件4.2・4.3・4.4・9.2の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後76件すべて失敗、dotnet終了2。ONで同じ76件成功後、一時flagを除去した。exec応答の終了1はdotnetの終了値と区別して記録した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,375件・76件・701件成功、失敗・スキップ0、各dotnet終了0。CSharpier9ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。差分と新規8テストの全文、演算順・符号解釈・trap条件・位置・残る値、RED/GREENを確認。既存4.1handlerの不変と独立ビルド・同じ3suiteの成功も確認した。
- 実装記録:`artifacts/numeric-control/task-4.2-20261011T205000/verification.md`。独立レビュー:`artifacts/numeric-control/task-4.2-review-20261011T212745/review.md`。各出力先にコマンド・rawlog・dotnet終了値を保持した。

親エージェントが8handlerと共有trapの差分、代表境界テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開診断・Invoke/start・生成dispatchと公式全体の統合確認は7.1・7.2で行う。

## タスク4.3:整数のビット操作と計数

両幅のand/or/xor・shift・rotate・clz/ctz/popcntの22handlerを追加した。移動量は31/63でmaskし、unsignedのビット解釈と対象幅の`BitOperations`を使う。計数も元の整数幅へ積む。変更は本体1ファイルと対象別テスト22クラス。要件4.5・4.6の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後118件中116件失敗・2件成功、dotnet終了2。入力と結果が同じpopcnt(0)の2件はOFFでも一致するが、他入力とclz/ctz(0)は失敗した。ONで同じ118件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,493件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier23ファイルと差分確認も終了0。
- 履歴なしの独立レビュー:`APPROVED`。差分と新規22テスト全文、幅・移動量・符号・方向・計数・残る値、RED/GREENを確認。既存36handler・IntegerTrap・fixtureの不変と、独立ビルド・同じ3suiteの成功も確認した。
- 実装記録:`artifacts/numeric-control/task-4.3-20261011-21ac6d/verification.md`。独立レビュー:`artifacts/numeric-control/task-4.3-review-20261011-8d24a1/review.md`。各出力先にコマンド・rawlog・終了値を保持した。

親エージェントが22handlerの差分、代表境界テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。整数58handlerの直接確認を終えた。公開opcodeの宣言・型検証・生成dispatchの接続は7.1、公開統合と公式全体の次回確認は7.2で行う。

## タスク5.1:浮動小数点算術と目的幅の丸め

新規の浮動小数点partialに両幅のadd/sub/mul/div/sqrtの10handlerを追加した。f32はfloat/MathF、f64はdouble/Mathで計算し、命令ごとに同幅の生ビットへ保存する。NaN結果だけを幅別private補助で正のcanonicalへ揃え、符号付き0・subnormal・無限大を保つ。変更は本体1ファイルと直接テスト10クラス。要件5.1・5.2・5.6・5.7の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後90件中74件失敗・16件成功、dotnet終了2。入力と結果が同じ特殊値はOFFでも一致するが、10クラスすべてで算術結果が失敗した。ONで同じ90件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,583件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier11ファイルと差分確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。新規11ファイル全文、対象幅・operand順序・丸め・特殊値・同contextの連続演算、NaN補正の境界とRED/GREENを確認。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-5.1-20261011T195103/verification.md`。独立レビュー:`artifacts/numeric-control/task-5.1-review-20261011T200600/review.md`。各出力先にコマンド・rawlog・終了値を保持した。

親エージェントが10handlerと幅別補助の全文、代表境界テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。opcode・型検証・生成dispatchの接続は7.1、公開統合と公式全体の次回確認は7.2で行う。

## タスク5.2:整数値化と符号bit操作

浮動小数点partialに両幅のceil/floor/trunc/nearest・abs/neg/copysignの14handlerを追加した。丸めはMathF/MathとToEvenを用い、幅別private補助で0の入力符号と既存NaN補正を適用する。符号操作は生ビットだけを変更してpayloadを保つ。変更は本体1ファイルと直接テスト14クラス。要件5.3・5.4・5.7の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後168件中122件失敗・46件成功、dotnet終了2。全14クラスで値不一致を確認し、保持系の入力はOFFでも自然に一致した。ONで同じ168件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,751件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier15ファイルと差分確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。新規14テスト全文と本体差分、方向・同距離・隣接値・0の符号・特殊値・payload・copysignの順序、既存契約、RED/GREENを確認。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-5.2-20261011T210205/verification.md`。独立レビュー:`artifacts/numeric-control/task-5.2-review-20261011T211700/review.md`。各出力先のcommandsにrawlog・コマンド・native終了値を保持した。

親エージェントが14handlerと幅別補助の最終差分、代表境界テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開命令の接続は7.1、公開統合と公式全体の次回確認は7.2で行う。

## タスク5.3:最小値・最大値と浮動小数点比較

両幅のmin/maxと6比較の16handlerを追加した。min/maxはNaNを先に補正し、等値の非NaNにOR/ANDを適用して±0の規則を保つ。比較は左右順の各幅演算でi32の0/1を返す。変更は本体1ファイルと直接テスト16クラス。要件5.5・5.7・5.8の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後200件中168件失敗・32件成功、dotnet終了2。全16クラスでmin/maxの値・NaN・0符号、比較の結果型不一致を検出した。ONで同じ200件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは1,951件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier17ファイルと差分確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。本体差分と新規16テスト全文、左右交換・特殊値・等値・結果型、既存処理の保持とRED/GREENを確認。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-5.3-20261011T211400/verification.md`。独立レビュー:`artifacts/numeric-control/task-5.3-review-20261011T221200/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが16handlerの最終差分、代表境界テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。浮動小数点40handlerの直接確認を終えた。公開命令の接続は7.1、公開統合と公式全体の次回確認は7.2で行う。

## タスク6.1:整数幅変換・符号拡張・再解釈

新規Conversionのpartialにwrap・signed/unsigned extend・指定下位幅の符号拡張8handlerと同幅の再解釈4handlerを追加した。指定幅のunchecked castと生ビットAPIを使い、再解釈は浮動小数点演算とNaN補正を通さない。変更は本体1ファイルと直接テスト12クラス。要件6.1・6.2・6.7の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後69件中55件失敗・14件成功、dotnet終了2。全12クラスで値の失敗を確認し、0出力と一致する入力だけが自然に成功した。ONで同じ69件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,020件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier13ファイルと差分確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。新規13ファイル全文、上位bit・幅と符号境界、±0・正負sNaN/qNaNの4方向と往復、生ビットAPIと既存処理の保持、RED/GREENを確認。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-6.1-20261011T231500/verification.md`。独立レビュー:`artifacts/numeric-control/task-6.1-review-20261011T233930/review.md`。各出力先のcommandsにコマンド・rawlog・native終了値を保持した。

親エージェントが12handlerの最終全文、代表境界・往復テスト、RED/GREENと現在のビルド・3suiteのrawlog・終了値、独立レビューを確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開命令の接続は7.1、公開統合と公式全体の次回確認は7.2で行う。

## タスク6.2:trapを伴う浮動小数点から整数への変換

全8組合せのtrunc_s/uを追加した。NaNを先に区別し、0方向への切捨て後の値を正確な2の冪による半開区間で検査してからcastする。共通trap補助を同じInterpreter型の`NumericTrap`へ先に移し、既存Div/Remの76件を確認した。変更は本体4ファイルと直接テスト8クラス。要件6.3・6.4と9.2の内部trap生成の範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後154件すべて失敗、dotnet終了2（tool終了1とは区別）。ONで同じ154件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,174件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier12ファイルと差分確認も成功した。
- 履歴なしの独立レビュー:`APPROVED`。全8テスト全文と本体差分、NaN優先・signed下限の小数・unsignedの負の小数・上下限の隣接値・trap理由と位置・下位値の保持を確認。全154期待値を製品castやMath.Truncateを使わずIEEEのビット列から独立に照合した。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-6.2-20261012T0015/verification.md`。独立レビュー:`artifacts/numeric-control/task-6.2-review-20261011T211823/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが最終実装と代表境界テスト、責務整理後76件・RED/GREEN・最終ビルドと3suiteのrawlog・終了値、独立レビューと期待値照合結果、検証後の12ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開命令の接続は7.1、公開診断・Invoke/startと公式全体の次回確認は7.2で行う。

## タスク6.3:飽和する浮動小数点から整数への変換

全8組合せのtrunc_sat_s/uを追加した。NaNは0、0方向への切捨て後の区間外は変換先の端を返し、範囲内でだけcastする。共通境界を4つのprivate上限定数へ先に整理して既存truncの154件を確認した。変更は本体1ファイルと直接テスト8クラス。要件6.5の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後154件中72件失敗・82件成功、dotnet終了2。全8クラスでNaN・範囲外・無限大が成功せずtrapになることを検出し、正常な共通入力は自然に成功した。ONで同じ154件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,328件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier9ファイルと差分確認も成功した。整形後は既存のLFを維持し、その最終状態で確認した。
- 履歴なしの独立レビュー:`APPROVED`。全8テスト全文、本体差分、NaN・切捨て後の半開区間・上下限飽和・trapなし・下位値の保持を確認。全154期待値をIEEEのビット列から有理数へ独立に展開して照合した。既存truncは定数抽出以外に変更がない。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-6.3-20261011T212800/verification.md`。独立レビュー:`artifacts/numeric-control/task-6.3-review-20261011T213928/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが8handlerと目的幅別補助の全文、代表境界テスト、先行整理154件・RED/GREEN・最終ビルドと3suiteのrawlog・終了値、独立レビューと期待値照合結果、検証後の9ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開接続は7.1、公開診断と公式全体の次回確認は7.2で行う。

## タスク6.4:整数convertと浮動小数点の幅変換

全8組合せの整数convertとdemote/promoteの10handlerを追加した。整数は符号解釈後に目的float/doubleへ直接castし、幅変換は既存のNaN補助へ接続する。変更は本体1ファイルと直接テスト10クラス。要件5.7・6.6の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後131件すべて失敗、dotnet終了2。全10クラスで変換前の結果型を検出した。ONで同じ131件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,459件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier11ファイルと差分確認も成功し、既存のLFを維持した。
- 履歴なしの独立レビュー:`APPROVED`。全10テスト全文、本体差分、符号解釈・整数の上下限・偶数と奇数のtie・幅変換の非正規化数・underflow/overflow・±0・無限大・NaNを確認。固定公式の二重丸め8入力とhexfloatから導いた目的bitを独立に照合した。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-6.4-20261011T214751/verification.md`。独立レビュー:`artifacts/numeric-control/task-6.4-review-20261011T220631/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが10handlerの最終差分、代表4クラスと公式8値の原文、RED/GREEN・最終ビルドと3suiteのrawlog・終了値、独立レビュー、検証後の11ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。数値handlerは整数58・浮動小数点40・変換38の計136が揃った。公開命令宣言・型検証・生成dispatchの接続は7.1、公開診断・公式全体の確認は7.2で行う。

## タスク7.1:数値命令の宣言・型検証・生成dispatch

通常0x45〜0xC4の128命令とFC0〜7の8命令を完成済みhandlerへ宣言し、FunctionValidatorはNumericSignatureから入力数・型と結果型を検証する。const4命令と定数式の既存規則を維持し、通常ビルドの生成dispatchを利用した。変更は本体2ファイルと関連テスト8ファイル。要件1.3・2.1・2.2・2.6・4.6・4.7・9.1の本タスクの範囲である。

- 実装者:`READY_FOR_REVIEW`。接続前のREDはビルド成功後207件中56件失敗、dotnet終了2。ONで同じ207件成功後、一時flagを除去した。全体テストで旧未対応fixture2件を追加検出し、停止位置・未確認範囲を保ってmemory.initへ置換して再確認した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,514件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier10ファイルと差分確認も成功し、既存のLFを維持した。
- 履歴なしの独立レビュー:`APPROVED`。全136属性を固定公式の番号・名前・型規則・handlerと照合し、比較のi32、i64計数のi64、FC全番号、constの維持を確認。新規公開テスト2ファイル、既存未対応fixture5ファイル、生成器の既存契約と定数式制限も確認した。独立ビルドと同じ3suiteも成功した。
- 実装記録:`artifacts/numeric-control/task-7.1-20261012T0500/verification.md`。独立レビュー:`artifacts/numeric-control/task-7.1-review-20261012T0645/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。実装者の最新ログはfinal2-*で、旧失敗ログも保持した。

親エージェントが数値宣言の代表箇所、型検証の最終差分、公開正常・型不成立・定数式テスト、全既存fixture差分、RED/GREENと最終・独立ビルド/3suiteのrawlog・終了値、独立レビュー、検証後の10ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開数値trap・start・固定147入力の全体実行は7.2で行う。

## タスク7.2:数値trapの公開診断と公式全体の確認

ExecutionBoundaryで0除算・整数overflow・不正整数変換をReason別の固定診断へ変換した。Invokeとstartの元の段階・位置を保持し、trap後のglobal更新とホスト呼出しが実行されないことを3Reasonずつ確認した。変更は本体1ファイルと関連テスト3ファイル。要件4.3・6.4・9.2・9.3・9.6・10.1・10.3・10.5・10.7の本タスクの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド成功後6件すべて診断先頭の不一致で失敗、dotnet終了2。ONで同じ6件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,520件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier4ファイル、CLI起動、差分確認も成功し、LFを維持した。
- 固定147入力の公式run:53,907commandの記録完了、passed15,939、failed/runner_error0、runtime_unsupported2,703、blocked34,188、out_of_scope1,077、終了0。保存baselineとの比較は成立・完了、変化13,474、追加・欠落・回帰・未比較0、終了0。素材・profile・期待値・分類・baselineは変更していない。
- 数値trap119件（0除算38・整数overflow41・不正整数変換40）はすべてpassedでReason・Invoke段階・固定診断先頭が一致した。conversions.wast全619件がpassed。数値136命令とconst4命令に対応する未対応Featureは0。blocked全34,188件のoriginがruntime_unsupportedである。
- 履歴なしの独立レビュー:`APPROVED`。全差分、6公開ケース、RED/GREENを確認し、独立ビルドと3suiteも成功した。公式結果と比較の全53,907件を元manifest・baselineへ独立に照合し、初期passed2,491件の維持、数値trap119件、blockedの全origin、最終4ファイルのhash一致を確認した。監査側の初回不一致は製品を変更せず監査処理を修正し、最終native終了0・passed=true・errors=[]を確認した。
- 実装記録:`artifacts/numeric-control/task-7.2-20261011T190000/verification.md`。公式結果と比較は同ディレクトリのrun.json/comparison.json。独立レビュー:`artifacts/numeric-control/task-7.2-review-20261011-independent-a1/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが公開境界の最終差分と関連テスト、RED/GREEN・最終/独立ビルドと3suiteのrawlog・終了値、公式結果と比較、独立全件監査、検証後の4ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。この判定は7.2の範囲に限定する。制御/select等の後続タスクと残存未対応があり、numeric-control全体とCore 2.0全体の完成は未判定である。

## タスク8.1:現在関数の分岐情報と保持値の移動

InterpreterContextにGetBranchTargetとBranchを追加した。末尾の保持値をOperandBase相対の高さへ順序を保って移し、不要範囲をclearし、ValueCountと現在frameのPcだけを更新する。既存fixtureへ分岐表を渡す末尾optional引数を追加した。変更は本体1ファイル、既存fixture1ファイル、新規内部テスト1ファイル。要件7.8・7.9と9.5の内部状態維持・復元の範囲である。

- 実装者:`READY_FOR_REVIEW`。修正後ビルドの警告・エラー0を確認してから既定OFFのREDを実行し、6件中分岐4件失敗・lookup2件成功、native終了2。ONで同じ6件成功後、一時flagを除去した。初回ビルドのusing不足4エラーと修正後の出力も保持した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,526件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier3ファイルと差分確認も成功し、LFを維持した。
- 新規6ケースは現在関数の分岐表、保持0/1/11、重なる移動・重ならない移動を確認する。全7値型の生NaN・負の0・v128の上下bit・null/非null参照を含め、値と参照の同一性、破棄スロットの解除、locals・外側operand・caller・frame数・深さの維持を確認した。Actとfinally復元はawait前の同期区間で行う。
- 履歴なしの独立レビュー:`APPROVED`。本体とfixtureの差分、新規テスト全文、仕様、RED/GREEN snapshotを確認した。独立ビルドと同じ3suite、整形、静的確認、差分確認も成功し、最終3ファイルがレビュー前・実装者保存・final snapshotのhashと一致した。
- 実装記録:`artifacts/numeric-control/task-8.1-20261011T195705-branch-7bfa/verification.md`。独立レビュー:`artifacts/numeric-control/task-8.1-review-20261011-independent/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが本体とfixtureの最終差分、新規テスト全文、RED/GREENと最終/独立ビルド・3suiteのrawlogとnative終了値、独立レビュー、最終hash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。公開branchの接続、失敗後の新しい独立呼出し経路、公式全体runは後続タスクの範囲である。

## タスク8.2:条件分岐・分岐表・nop・selectの内部handler

Interpreter.ControlにNoOp、Branch、BranchIfZero、BranchIf、BranchTable、Selectを追加した。分岐値の移動は既存Contextへ接続し、条件と選択値を消費して確定済み分岐先を選ぶ。Selectは選択したWasmValueをそのまま積む。変更は本体1ファイル、同期実行用fixture1ファイル、メソッド別の直接テスト6ファイル。要件7.1・7.3・7.4・7.5・7.6・7.7・8.1の内部handlerの範囲である。

- 実装者:`READY_FOR_REVIEW`。既定OFFのREDはビルド警告・エラー0後、32件中31件失敗・状態不変のNoOp1件成功、native終了2。ONで同じ32件成功後、一時flagを除去した。
- 最終Releaseビルド:終了0、警告・エラー0。3suiteは2,558件・76件・701件成功、失敗・スキップ0、各終了0。CSharpier8ファイルと既存/新規差分の空白確認も成功し、LFを維持した。
- 直接テストは前方/後方分岐の32回反復、条件0/1/−1と非分岐の値保持、分岐表の候補順・開始添字・境界/負値/空候補の既定分岐を確認した。Selectは4スカラーの16ケースで生NaN payloadと±0のbitを保持し、NoOpは値・Pc・観測状態を維持する。全handlerで下位値・local・frame数・深さ・処理段階を確認した。
- 履歴なしの独立レビュー:`APPROVED`。全8未追跡ファイルの差分と全文、既存契約、要件・設計、RED/GREEN snapshotを確認した。独立ビルドと同じ3suite、整形・静的確認・差分確認も成功し、最終8ファイルのhashが実装者保存と一致し、レビュー前後で不変だった。
- 実装記録:`artifacts/numeric-control/task-8.2-20261011T082000/verification.md`。独立レビュー:`artifacts/numeric-control/task-8.2-review-20261011T090100/review.md`。各出力先にコマンド・rawlog・native終了値を保持した。

親エージェントが6handler・fixture・全6テストの全文、RED/GREENと最終/独立ビルド・3suiteのrawlogとnative終了値、独立レビュー、最終8ファイルのhash一致を確認し、`kiro-verify-completion`は`TASK: VERIFIED`。命令宣言・Decode・Validate・公開実行への接続、参照/v128 selectの未対応判定、公式全体runは後続9系タスクの範囲である。
