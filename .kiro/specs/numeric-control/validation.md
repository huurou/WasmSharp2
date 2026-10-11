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
