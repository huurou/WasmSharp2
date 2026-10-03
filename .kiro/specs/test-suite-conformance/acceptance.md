# 修正・比較の準備記録

## 対象と前提

2026-10-03にタスク1の準備を行った。作業開始時のrevisionは`b7afecd75c7ab3f3f2336880841898c33181ce1e`で、未コミット変更はなかった。本記録と`remediation.json`、タスク状態、上流の最終検証完了を反映するロードマップの1項目を変更した。ランタイム・ランナー・テスト・固定素材・現baselineへの変更はない。

作業開始時点では、[上流タスク記録](../test-suite-runner/tasks.md#implementation-notes)の2026-10-03の記録とロードマップに、`test-suite-runner`の仕様全体の最終実装検証が未実施と明記されていた。そのため初回公式受入や過去のGOを代用せず、今回の通常検証と現行ランナーの全体実行を根拠に上流仕様の`kiro-validate-impl`を行った。独立した検証で`GO`となり、タスク1.1の上流完了前提を確認した。過去の受入記録や承認状態は変更していない。

## 通常の実行環境

| 項目 | 確認結果 |
| --- | --- |
| SDK | `dotnet --version`: `10.0.401`、終了0 |
| 固定参照ソース | specのHEADは`05ca4182176763112561ae20153975c12bd689e4`、WABTのHEADは`03a00a1334e6121fb0cce4fccbd6bb109b68acaa` |
| 既存テスト | ランタイム・生成器・ランナーの3つのTUnitプロジェクトがReleaseビルドで生成された |
| ローカルツール | `dotnet tool list --local`: CSharpier`1.3.0`、Husky`0.9.1` |
| 通常ビルド | `dotnet build WasmSharp2.slnx -c Release --warnaserror --nologo`: 終了0、警告0、エラー0 |
| 起動確認 | `tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe --help`: 終了0、6操作の使用法を表示 |
| 整形確認 | `dotnet csharpier check .`: 終了0、378ファイルを確認 |

通常ビルド出力を後続の修正確認に使用する。`portable/runner/`の旧コピーは使わない。生成済み素材の実行にWABTのビルドは要求しない。

## 上流仕様の最終実装検証

[上流検証レポート](../../../artifacts/test-suite-conformance/task-1-20261003/upstream-validation/validation.md)に全14要件・111受入基準と実装の対応、設計・統合契約・責務境界、完了タスク、現在の検証結果を記録した。`kiro-validate-impl`の判定は`GO`、`kiro-verify-completion`の`FEATURE_GO`も`VERIFIED`だった。

現在の通常ビルドのランナーで、確保したmanifestを`run`し、元の全体baselineと`compare-run`した。結果は`artifacts/test-suite-conformance/task-1-20261003/upstream-validation/`の`run.json`と`compare-run.json`、各`.log/.exit`に保存した。

- 全147入力・53,907commandの記録・出力が完了し、入力異常・中断・欠落・重複・未処理・件数未確定・runner_errorは0だった。
- 必須66ケースはすべてpassed。blockedの全起点は同じ入力内の先行runtime_unsupportedへ到達した。
- 元baselineとの比較は成立・完了し、条件・出典・素材の差、変化・追加・欠落・回帰・未比較はすべて0だった。
- 分類件数は元baselineと同じだった。run・compare-runの終了1は既知failed944件のため、verifyの終了1はfailed・runtime_unsupported・blockedのためで、上流の初回受入・継続利用の契約に一致した。

この結果から、ロードマップの`test-suite-runner`だけを完了へ反映した。本仕様の診断修正やCore 2.0全件合格はまだ成立していない。タスク1.1の前提確認であり、現baselineを新しい結果へ更新していない。

## 修正前の比較元と固定素材

引継ぎ指定の資料をローカルで取得できたため、旧revisionの作業環境・素材の再作成は不要だった。診断一覧や修正後の結果で比較元を代用していない。

| 資料 | リポジトリrootからのpath | SHA-256 |
| --- | --- | --- |
| 全体baseline | `artifacts/test-suite-runner-acceptance-20261001/run-baseline.json` | `032cc64b6c088e920331b420490617da6094454ab08a52381ab7c35b1be3c057` |
| 移動後の全体結果 | `artifacts/test-suite-runner-acceptance-20261001/run-moved.json` | baselineと同一 |
| 生成manifest | `artifacts/test-suite-runner-acceptance-20261001/portable/corpus/manifest.json` | `5ce710f306e91f380ff45797aafeb7a0fbfd66882ce45a6d4bc63f5c27de9b6e` |
| 診断一覧 | `.kiro/specs/test-suite-conformance/diagnostic-groups.json` | `a4e9869801c1e77e654c0e0379a9aeb89130769f84005d174c37d2e59c657637` |

比較元のrun IDは`c8ebac70-d9fe-4f68-808b-a6791e63171e`、観測revisionは`1bda5b9368f04541676213ea90a9418e1f241751`、MaxCallDepthは1024である。profileは`core2`で、spec・WABTの固定commit、全feature設定、入力・生成物の一覧がbaseline内のmanifestと一致した。変換器の記録されたSHA-256は`b0e1d0316a265f659566a0e3b6f1a00fea9e315e3d25f2c3e0853e414d0be245`である。

`remediation.json`の`baseline_before`は、同じバイト列を保持する`run-moved.json`を修正前の観測参照として使う。`run-baseline.json`は後続タスク7で明示更新する比較用baselineであり、観測参照とは分ける。更新後も944ケースの元のCaseResultとhashを辿れるよう、`run-moved.json`は上書きしない。

当初の作業記録では、保存資料の監査を`artifacts/test-suite-conformance/task-1-20261003/audit-sources.ps1`で実行し、終了0、`source-audit.json`のissue_countは0だったと記載している。初回の終了値ファイルは未保存であり、今回の再監査の結果は[レビュー時の証跡確認](#レビュー時の証跡確認)に記録する。

- 全147入力の元WASTと5,821生成物のSHA-256がmanifestと一致した。147JSONのcommand数・種類・連続した0始まりindexを確認した。
- manifestと全体結果の53,907CaseIdが1対1で対応し、欠落・余剰・重複・未処理・件数未確定・入力異常は0だった。
- passed=1,547、failed=944、runtime_unsupported=2,987、runner_error=0、out_of_scope=1,077、blocked=47,352だった。
- 全944failedと102観測群を元入力の相対path・command indexで結合し、種類・行・期待診断・処理段階・公開例外型・Message・Reason・Location・import情報が一致した。必須66ケースも一意に対応し、すべてpassedだった。
- baselineのSHA-256は確認前後で同一だった。保存済み結果の監査であり、新しい公式runや修正後の受入ではない。

監査用の出力ディレクトリ作成はサンドボックス内で`Access denied`となった。権限を調整して同じリポジトリ内の専用ディレクトリへ作成・保存した。資料の欠落や製品の不具合として扱っていない。

## 修正記録の運用

[remediation.json](remediation.json)で944CaseIdから観測群・原因候補へ辿れるようにする。期待・実際・段階・診断は比較元のCaseResultを参照し、診断全文の手入力コピーを増やさない。観測群IDと原因IDは別に管理し、同じ観測群に複数原因が関係する場合は原因ID一覧を使う。

原因候補には担当境界、固定規則の出典、修正先、最小再現入力の方針、既存のテストファイル・クラスを対応付ける。静的に確認できた差と、944件それぞれの原因確定は区別する。変更タスクで最小入力を確定し、直接テストと公式run・比較の結果を追記してから状態を進める。

23件の調査候補を作成した。全候補・全ケースは`uninvestigated`で、`confirmed_static_difference`は現在のコードと固定規則の差だけを示す。`cause`は未確定、最小入力の構成案は`not_executed`、修正・受入の結果参照は空である。検証全体とリンクの検査順に関するF014/F022は関連観測群を持つが、全ケースの原因として自動割当てしていない。各ケースの`finding_ids`も確定原因ではなく調査候補として扱う。

F002/F003はsectionをまたぐLEBのD026を共通の調査候補として参照する。F014/F022は検査順の差を示す複合ケースが未観測のため、`basis_case`をnullとし、構成案と固定規則を根拠に調査する。F010の`rule`は参照の末尾検査を記録し、確定済み件数不一致を未対応へ変えない本仕様の早期検査は`design_decision`へ分けて記録する。

当初の作業記録では、`artifacts/test-suite-conformance/task-1-20261003/audit-remediation.ps1`で全944CaseIdの一意性・観測群・source・候補ID、固定資料・修正先の存在、既存テストファイル内のクラス名を確認し、終了0、`remediation-audit.json`のissue_countは0だったと記載している。初回の終了値ファイルは未保存であり、旧スクリプトの追加確認はメモリ内のcase・transferの件数確認にとどまっていた。参照・状態・JSON往復を補強した現行版での再監査は[レビュー時の証跡確認](#レビュー時の証跡確認)に分けて記録する。

この`audit-remediation.ps1`はタスク1の準備時点専用であり、944件・全件未調査・引継ぎ0を前提とする。追加例の固定件数も監査用コピーだけの確認条件である。タスク2以降で状態や件数を進めた修正記録に、この準備監査の成功を要求しない。

新しい不一致は`cases`へCaseIdと新しい全体結果のsource参照を追加する。境界外の原因は`transfer`へ所有仕様・原因・必要機能・根拠ケースを記録する。未調査・原因確定・修正済み・確認済み・境界外への引継ぎを区別し、引継ぎを解消やpassedとして扱わない。

追加時のキーは次のとおり。新しいsource・候補を先に登録し、CaseIdと状態の参照を保つ。JSONの追加検証は監査用コピーだけで行い、架空ケースは保存しない。

| 要素 | キーと参照 |
| --- | --- |
| sources | `id`、`role`、`path`、`sha256`と該当資料の`run_id`・`revision`。ケースが参照するIDは全体結果のsourceを指す。 |
| cases | `input_path`、0始まり`command_index`、登録済み`source_id`、`observation_group_id`（未集約ならnull）、登録済み`finding_ids`、`status_definitions`にある`status` |
| findings | 引継ぎ時は`status: transferred`とし、`owner_spec`を引継ぎ先へ更新する。`transfer`には最上位transferの`finding_id`を文字列で参照し、引継ぎ前はnullとする。`cause`は確認した原因を記録する。 |
| transfer | 登録済み`finding_id`、`owner_spec`、`cause`、`required_feature`、記録済みCaseIdの`evidence_case`、`status: transferred`。対応する候補・ケースも引継ぎ状態へ更新する。 |

後続の修正では[引継ぎ手順](handoff.md#受入資料と再開方法)の通常ランナーによる全体runとcompare-runを使用し、今回確認したbaselineを比較元へそのまま渡す。タスク1ではbaseline-saveを行わない。

## 独立レビューと通常検証

以下は当初の作業記録を保持したものであり、判定原文を本レビューで取得したものではない。保存資料による確認限界と今回の検証は、末尾の「レビュー時の証跡確認」に分けて記録する。

タスク1.1の独立した`kiro-review`も`APPROVED`だった。新しい上流GOと111受入基準の対応表、全51チェック項目の完了、通常環境・ビルド・起動の証跡を確認した。ロードマップ1項目の反映も前提完了の記録として妥当と判定された。

タスク1.2の独立した`kiro-review`は`APPROVED`だった。実ファイルのhashとmanifest・CaseIdを照合し、監査も別の専用出力先で再実行してissue_count=0を確認した。通常検証のログとTRXは`artifacts/test-suite-conformance/task-1-20261003/review-1-2/`に保存した。

タスク1.3も別の独立した`kiro-review`で`APPROVED`だった。全53,907CaseResultから944failed・102観測群・修正記録944件を照合し、固定参照ソースと既存コードから23候補の静的差・修正先・最小入力案を確認した。新規source・finding・caseと境界外transferの追加・JSON往復もメモリ内で確認した。記録のみの変更で通常検証後のコード差分がないため、同じビルド・テストの重複実行はしなかった。親も現在のbaselineのhash、ビルド・テストの終了値とTRX、監査結果を確認し、両タスクの準備完了を`kiro-verify-completion`の`TASK`として確認した。

| コマンド | 結果 |
| --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --warnaserror --nologo` | 終了0、警告0、エラー0 |
| `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/test-suite-conformance/task-1-20261003/review-1-2/runtime` | 終了0、975成功、失敗0、スキップ0 |
| `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/test-suite-conformance/task-1-20261003/review-1-2/generator` | 終了0、37成功、失敗0、スキップ0 |
| `dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory artifacts/test-suite-conformance/task-1-20261003/review-1-2/runner` | 終了0、701成功、失敗0、スキップ0 |
| `dotnet csharpier check .`、通常ビルドのランナー`--help`、`git -c core.excludesFile= diff --check` | すべて終了0 |

通常テストだけでは上流仕様の最終実装検証を代替せず、上記の要件・設計・統合契約と公式全体結果を合わせて上流の判定を行った。GitHub Actionsそのものは今回実行していない。本仕様の修正後受入とCore 2.0全件合格もまだ成立していない。

最終確認では、現在のbaselineのSHA-256、修正記録の944件・23候補、上流GOの正式レポート、ビルド・テスト・監査・全体比較の証跡、独立レビュー3件の未解消指摘0を確認した。`kiro-verify-completion`の`TASK`としてタスク1.1〜1.3の準備完了を`VERIFIED`とし、タスク1を完了へ更新した。タスク2以降のランタイム修正と、本仕様全体の`kiro-validate-impl`は未実施である。

### レビュー時の証跡確認

上記の独立レビュー3件のAPPROVEDとタスク完了VERIFIEDは、当初の作業記録に記載された判定である。判定原文と初回2監査の終了値ファイルは保存資料に見当たらず、保存資料だけでは当時の独立性・判定過程・監査終了値を再確認できない。過去の原文や終了値を後から作成せず、本レビューでの独立した記録照合・監査再実行・再レビューを区別して残す。

Claude Codeの初回読み取り専用レビューではMedium2件・Low6件が指摘され、その対応として参照維持、上流の状態記録、候補の関連付け・参照規則・根拠ケース、追加記録の監査、確認限界を補正した。CLIは終了0、最終resultはsuccessだった。別コンテキストの記録監査でも全53,907ケースのbaseline・current・比較詳細、944件・102群・23候補、hash、必須66ケースとblockedの起点を照合し、問題0だった。当初のインラインPython照合は子エージェントの会話内にだけ出力があり、スクリプト・出力ファイルは保存していなかった。今回の独立監査は別途実行し、`claude-review/independent-record-audit.py`と`.json/.log/.exit`へ保存した。当時の独立レビュー原文の復元ではない。

今回の補正対象は本仕様の`acceptance.md`・`remediation.json`・`tasks.md`、上流`test-suite-runner/tasks.md`、`roadmap.md`の概要と上流完了項目、ローカルの2監査スクリプトである。冒頭に記した当初のタスク1の変更範囲とは区別する。

今回のコマンドと出力は`artifacts/test-suite-conformance/task-1-20261003/claude-review/`に別名で保存した。通常コード・テストには差分がないため、テストと公式run・compare-runは再実行していない。既存テストの975/37/701成功・失敗0・スキップ0は保存済みログとTRXで確認した。

| コマンド | 今回の結果 |
| --- | --- |
| `dotnet build WasmSharp2.slnx -c Release --no-incremental --warnaserror --nologo` | 終了0、警告0、エラー0。`build.log`・`build.exit`に保存 |
| `pwsh -NoProfile -File artifacts/test-suite-conformance/task-1-20261003/audit-sources.ps1 -OutputPath artifacts/test-suite-conformance/task-1-20261003/claude-review/source-audit.json` | 終了0、issue_count=0。147入力・5,821生成物・53,907ケース・944診断・必須66ケースを照合。`.log`・`.exit`も保存 |
| `pwsh -NoProfile -File artifacts/test-suite-conformance/task-1-20261003/audit-remediation.ps1 -OutputPath artifacts/test-suite-conformance/task-1-20261003/claude-review/remediation-audit-final.json` | 終了0、issue_count=0。最終版の944件・23候補と、追加参照・状態・JSON往復、未登録ID・未定義状態・引継ぎを確認済みへ変える入力の検出を確認。`.log`・`.exit`も保存 |
| `python -X utf8 artifacts/test-suite-conformance/task-1-20261003/claude-review/independent-record-audit.py` | 終了0、issue_count=0。53,907ケースの結果・比較詳細、944件・102群・23候補の参照と状態、修正した関連付け、必須66ケース、47,352blockedの起点、3sourceのhashを独立して照合。`.json/.log/.exit`に保存 |

監査スクリプトの`OutputPath`は、元の監査結果を上書きせず今回の証跡を保存するために追加した。初回の専用ディレクトリ作成はサンドボックスのアクセス拒否で監査開始前に終了1となり、上表は権限を調整した再実行の結果である。架空の資料・候補・ケース・引継ぎはメモリ内だけに置き、修正記録へ保存していない。

今回の監査スクリプト2本は`claude-review/audit-script-hashes.json`に元path・SHA-256と版の保存先を記録する。最終監査より前の`remediation-audit.json`は初回補正時、`remediation-audit-final-round2.*`は再レビュー前の結果として保持し、現在の修正記録は`remediation-audit-final.*`で確認する。

独立監査のJSONには使用スクリプトと修正記録のSHA-256も保存した。147WAST・5,821生成物のhashは上記source監査で確認済みのため重複監査せず、23候補の参照規則・最小入力案の意味論と過去承認の本人性は独立監査スクリプトの対象外とする。
