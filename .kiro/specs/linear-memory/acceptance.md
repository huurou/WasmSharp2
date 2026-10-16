# linear-memoryの受入記録

タスク1.1の記録は当時の状態を保持している。
現在の固定素材は「DataCount修正後の素材と比較元」、最終受入の照合は「8.3の最終受入照合」、次機能の比較元は「8.4の明示baseline保存」を参照する。

## 確認した範囲

2026-10-16（JST）にタスク1.1の環境・固定素材・比較元を確認した。
開始時のHEADは`cff8e290e099af71ada23a73e5b11979e49ad0a4`で、未コミット変更はなかった。
今回の変更は本記録と、Git管理外の[専用証拠ディレクトリ](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/)への出力である。

本仕様の線形メモリ機能の受入判定はまだ行っていない。
今回の検証では公式スイートのrun・compare-run・baseline-saveを実行しておらず、素材再生成、比較条件変更、実行結果baselineの更新も行っていない。
54受入基準と公式ケース・直接テストの対応、本仕様の対象ケースの合格、残存未対応の原因、回帰0件の判定は、後続タスクで確認する。

| 参照 | タスク1.1で確認した内容 | 残る受入確認 |
| --- | --- | --- |
| 要件8.1 | 固定したCore 2.0全体の入力と既存ランナーを利用できる | 本仕様の実装後の全体実行、期待値・段階・分類・診断先頭の一致 |
| 要件8.3 | 直前に明示保存された、同じ固定素材の比較元を特定した | 全体実行後の比較、以前passedだったケースの退行・欠落0件 |
| 要件8.6 | 比較元を保持し、CLIに表示されるbaseline-saveの説明と先行保存証拠を確認した | 受入後、比較済みの同じ全体実行結果を明示保存すること |

設計の「責務境界（Boundary Commitments）」「ConformanceAcceptance」「検証方針（Testing Strategy）」「実行順序とコマンド」に従った。
コード・テストの追加や変更を伴わない準備タスクのため、feature flagとREDは対象外である。

## 検証環境

| 項目 | 実測・確認結果 |
| --- | --- |
| 作業ディレクトリ | `D:\source\repos\WasmSharp2` |
| OS / RID | Windows`10.0.26300` / `win-x64` |
| .NET SDK | `10.0.402`、MSBuild`18.9.15+a8dd0577c` |
| .NET Host | `10.0.13`、x64 |
| テスト | 既存3プロジェクトはいずれも`net10.0`、TUnit`1.66.16` |
| 生成器 | 本体は`netstandard2.0`、生成器テストは`net10.0` |

SDK情報を表示したコマンドの出力は[dotnet-info.log](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/dotnet-info.log)、環境確認コマンドと終了値は[environment.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/environment.json)に保存した。
HEADとsubmoduleの状態も同じ証拠ディレクトリに保存した。
開始時の`git status --porcelain=v1`は確認して空だったが、コマンドの出力はファイルに保存していない。

## 固定版と素材

| 対象 | 確認結果 |
| --- | --- |
| Core 2.0仕様・公式テスト | `thirdParties/WebAssembly-spec`、commit`05ca4182176763112561ae20153975c12bd689e4` |
| WABT | `thirdParties/wabt`、1.0.42、commit`ff0ef7e0009402740c805a9744c09b05be063e48` |
| WABT実行ファイル | `artifacts/wabt-core2-1.0.42/Release/wast2json.exe` |
| WABT実行ファイルのSHA-256 | `5c377ca185191854aa5e29c8f96e9fe347730d964185adfc6a859ecb5ffbabf0` |
| profile | `tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json`、ID`core2` |
| profileのSHA-256 | `b9e4af9a40a6fefb9d0e4689c82e340043bdcadf9f95ed63b21ce2f5df082284` |
| 公式入力 | `thirdParties/WebAssembly-spec/test/core/**/*.wast`の147件、うちSIMD配下57件 |
| manifest | `artifacts/numeric-control/wabt-1.0.42-20261012/corpus/manifest.json` |
| manifestのSHA-256 | `4979e98df30308d4418e3050899f322ec1b085b88b7d2b3c053e2f9e0abe1d3b` |
| 生成物 | manifestからの相対配置`modules/`に5,821件。JSON147件、Wasm4,597件、WAT1,077件 |

親リポジトリのgitlinkと両submoduleのHEADは、上記の固定commitと一致した。
現在のprofileとmanifest内のprofileは、全21機能、147入力のパス・SHA-256、論理引数、変換設定を含めて一致した。
元WAST147件と生成物5,821件を個別にハッシュ照合し、不一致は0件だった。
全生成物と元入力の対応は[input-audit.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/input-audit.json)の`inputs`・`artifacts`に保存した。
WAT1,077件は素材同定のためハッシュ照合したものであり、ランタイムの実行成功件数には含めない。

| 実効状態 | 機能 |
| --- | --- |
| ON | `mutable-globals`、`saturating-float-to-int`、`sign-extension`、`simd`、`multi-value`、`bulk-memory`、`reference-types` |
| OFF | `exceptions`、`threads`、`function-references`、`tail-call`、`annotations`、`code-metadata`、`gc`、`memory64`、`multi-memory`、`extended-const`、`relaxed-simd`、`custom-page-sizes`、`compact-imports`、`wide-arithmetic` |

変換時の作業ディレクトリは`test/core`で、各入力の保存済み引数列は次の論理引数と一致した。
変換設定は`check=true`、`canonical_lebs=true`、`relocatable=false`、`debug_names=false`である。

```text
<relative.wast> --disable-exceptions --disable-tail-call --disable-annotations --disable-memory64 --disable-multi-memory --disable-extended-const --disable-relaxed-simd -o <output-root>/modules/<relative.json>
```

既存WABTのCMakeCacheはVisual Studio 18 2026・x64、`BUILD_TESTS=OFF`、`BUILD_LIBWASM=OFF`、`WITH_WASI=OFF`、`USE_INTERNAL_SHA256=ON`、`WERROR=ON`だった。
実行ファイルの版表示は`1.0.42 (git~1.0.41-80-gff0ef7e)`で、終了0だった。
実行ファイルのSHA-256はmanifestの出典情報と[WABT移行記録](../test-suite-runner/wabt-1.0.42-migration.md)の記録に一致した。
WABTの再ビルドや素材の再生成は行っていない。

## 今回の比較元

比較元には[numeric-controlの11.4の明示保存記録](../numeric-control/acceptance.md#114の明示baseline保存と次の比較元)で次の機能向けに指定されたbaselineを選んだ。
ロードマップと受入記録を照合した結果、統合先の先行機能はnumeric-controlまで完了しており、tables-referencesとsimdには受入記録がなかった。
ファイルの日時で比較元を選んでいない。

| 項目 | 確認結果 |
| --- | --- |
| 保存元の仕様 | `numeric-control`、タスク11.4 |
| 比較元 | `artifacts/numeric-control/task-11.4-20261012T-baseline-7ca484f/run-baseline.json` |
| SHA-256 / サイズ | `02ab113a70b5dd1c6ceb5aca12c901dddb8735b1754a035c999953baae9ad779` / 69,748,280byte |
| 対応manifest | `artifacts/numeric-control/wabt-1.0.42-20261012/corpus/manifest.json` |
| manifestのSHA-256 | `4979e98df30308d4418e3050899f322ec1b085b88b7d2b3c053e2f9e0abe1d3b` |
| 保存元run | `artifacts/numeric-control/task-11.1-20261012T003403Z/run.json` |
| run ID | `37925703-2880-4546-9550-cde250e6850b` |
| 実行ポリシー | `max_call_depth=1024` |

比較元のSHA-256は先行受入記録と設計に記載された値に一致し、保存元runのSHA-256とも一致した。
baselineに埋め込まれた`corpus`と対応manifestは、出典情報と全入力・生成物の一覧を含めて一致した。
先行仕様の[baseline-save.result.json](../../../artifacts/numeric-control/task-11.4-20261012T-baseline-7ca484f/baseline-save.result.json)に記録された保存操作は終了0である。
これは今回新しくbaseline-saveを実行した結果ではない。

保存済みbaselineは147入力・53,907commandの処理と出力が完了している。
分類はpassed16,924、failed0、runner_error0、runtime_unsupported2,100、blocked33,806、out_of_scope1,077である。
これらは先行仕様の保存済み結果であり、linear-memory実装後の実行結果としては扱わない。
test-suite-conformanceの旧baselineとWABT移行直後のbaselineは、numeric-control最終受入より前の比較元なので、今回の比較元には選ばなかった。

## 実行したコマンドと結果

Releaseビルドの終了0・警告0件・エラー0件を確認した後、以下の3テストプロジェクトを順番に実行した。
同じビルド出力を使うビルド・テストを同時実行していない。

```powershell
$evidence = 'D:\source\repos\WasmSharp2\artifacts\linear-memory\task-1.1-20261015T151304Z'
dotnet build WasmSharp2.slnx -c Release --warnaserror --disable-build-servers
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/runtime-trx"
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/generators-trx"
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/runner-trx"
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe --help
```

| 検証 | 終了値 | 件数・結果 | 実行コマンド・UTC・終了値 | テスト結果 |
| --- | --- | --- | --- | --- |
| Releaseビルド | 0 | 警告0、エラー0 | [build.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/build.result.json) | [標準出力](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/build.stdout.log) |
| ランタイム | 0 | 成功2,824、失敗0、スキップ0 | [runtime.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/runtime.result.json) | [TRX](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/runtime-trx/WasmSharp.Tests_net10.0_x64.trx) |
| 生成器 | 0 | 成功76、失敗0、スキップ0 | [generators.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/generators.result.json) | [TRX](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/generators-trx/WasmSharp.Generators.Tests_net10.0_x64.trx) |
| 公式ランナー | 0 | 成功702、失敗0、スキップ0 | [runner.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/runner.result.json) | [TRX](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/runner-trx/WasmSharp.TestSuiteRunner.Tests_net10.0_x64.trx) |
| CLI起動 | 0 | 既存6操作の使用法を表示 | [smoke.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/smoke.result.json) | [標準出力](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/smoke.stdout.log) |
| 固定素材の補助監査 | 0 | 147入力・5,821生成物、問題0 | [input-audit.result.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/input-audit.result.json) | [監査JSON](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/input-audit.json) |

全3プロジェクトの合計は3,602件成功、失敗0件、スキップ0件だった。
各行の標準出力・標準エラーは、同じ証拠ディレクトリの`<検証名>.stdout.log`・`<検証名>.stderr.log`に保存した。
本記録と証拠ファイルのSHA-256は[evidence-sha256.json](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/evidence-sha256.json)に保存する。
補助監査の再実行用スクリプトは[audit-inputs.py](../../../artifacts/linear-memory/task-1.1-20261015T151304Z/audit-inputs.py)で、実行コマンドは次のとおりである。

```powershell
python -X utf8 artifacts/linear-memory/task-1.1-20261015T151304Z/audit-inputs.py
```

初回の補助監査はWABTの版表示全体を文字列`1.0.42`と比較したため、終了1になった。
コマンドが出力したgit接尾辞と、固定commit・実行ファイルのハッシュ一致を確認し、版の先頭要素を照合するよう監査側だけを訂正した。
初回のスクリプト・JSON・実行ログ・終了値は同ディレクトリの`initial-*`に保持し、訂正後の終了0と区別した。
素材・期待値・分類・baselineの比較条件は変更していない。

## DataCount修正後の素材と比較元

2026-10-16、ユーザー承認に基づいてWABT1.0.42のDataCount出力を修正し、タスク6.1の変換不具合による停止条件を解消した。
修正、WABT自身の検証結果と既存不一致、比較元を移行した根拠は[test-suite-runnerの移行記録](../test-suite-runner/wabt-datacount-migration.md)に記載した。
本節は前提工程の記録であり、タスク6.1やlinear-memory全体の受入完了を示すものではない。

パッチ適用状態での初回検証では、`artifacts/linear-memory/wabt-datacount-20261016/`に次の組を保存した。現在の組は後述の「WABT修正コミットの固定後」を参照する。

| 用途 | 相対パス | SHA-256 |
| --- | --- | --- |
| 素材 | `corpus/manifest.json` | `13ab25b6adcfaed009fdc01cea33818847c911f0b7d4aa0dd5d1b26f481d1b84` |
| 変換baseline | `conversion-baseline.json` | `13ab25b6adcfaed009fdc01cea33818847c911f0b7d4aa0dd5d1b26f481d1b84` |
| 実行結果baseline | `run-baseline.json` | `70fc7bd5a5fc44225272552950ed45efd25f2f6ae45c05cb660a4850dad1f968` |

実行結果baselineはnumeric-controlの受入済みrevision`5287c36d7c2a90cc836f4ff2b23b91bffc279f53`から作成した。
旧素材で旧baselineとの全53,907ケースの一致を確認し、新素材で既存passed16,924件と全結果分類の維持を確認した後、別ファイルへ保存した。
旧素材・旧baselineは保持した。

実装途中のlinear-memoryを新素材で実行した結果は、同ディレクトリの`run.json`と`comparison.json`である。
Releaseビルドは警告・エラー0件、その後の3テストプロジェクトは計4,013件成功、失敗・skip0件だった。
公式全体実行と移行後baselineとの比較は終了0で、failed・runner_error・回帰・欠落・未比較は0件だった。
問題のmemory_init.wast#128と#136は、Decodeを通過し、Validateで期待した診断となって合格した。
全体にはruntime_unsupported1,631件とblocked32,744件が残るため、Core 2.0全件合格とは扱わない。
本仕様の実装途中のrunを受入baselineとして保存する操作は行っていない。

### WABT修正コミットの固定後

同日、ユーザー指示によりWABT本体の変更をcommit`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`へ保存し、親gitlinkとprofileを更新した。
以後は`artifacts/linear-memory/wabt-commit-20261016/`の次の組を使う。

| 用途 | 相対パス | SHA-256 |
| --- | --- | --- |
| 素材 | `corpus/manifest.json` | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 変換baseline | `conversion-baseline.json` | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 実行結果baseline | `run-baseline.json` | `619961b91dfa83c233864799e7f87323f811e5889c3d6faa7b460812fb72c4f5` |

受入済みrevisionの埋込みprofileだけを新WABT commitに合わせて再実行した。
直前の比較元と全53,907ケースが一致し、現在の実装のrunも直前の全ケースと一致した。
現在のrunと上表の比較元のcompare-runは終了0で、failed・runner_error・回帰は0件だった。
修正前の素材・比較元とパッチ適用状態での素材・比較元は、いずれも保持した。
詳細は[test-suite-runnerの移行記録](../test-suite-runner/wabt-datacount-migration.md)と[commit-audit.json](../../../artifacts/linear-memory/wabt-commit-20261016/commit-audit.json)を参照する。

## 8.3の最終受入照合

2026-10-16（JST）、HEAD`f6f638d471ac8c2175fbfeb6732beb381eb2fbea`で、線形メモリの実装挙動と回帰比較の受入条件を照合した。
要件1.1～8.5の53項目は、公式ケースまたは成功済みの直接テストと検証記録へ対応付けた。
**8.3照合時点では、要件8.6の保存はタスク8.4で実施予定であり、未完了だった。**
本節の判定は8.3の担当範囲に限る。
後続機能に依存する未対応と前提不成立が残るため、Core 2.0全体の完成を意味しない。

設計の「責務境界（Boundary Commitments）」「要件対応（Requirements Traceability）」「ConformanceAcceptance」「検証方針（Testing Strategy）」に照合した。
製品・テスト・ランナーの期待値と分類・検証プロファイル・固定素材・比較元は変更していない。
文書と保存済み証拠の監査だけを行い、REDはN/Aとした。
製品とテストを変更していないため、8.2の独立レビューで実行した全テストと公式run/compareを用い、同じ実行を繰り返していない。

### 正式runと比較元

8.2の独立レビューでfresh Releaseビルドと全4,084テストの成功後に実行した、次の組を正式候補とする。
比較元はnumeric-controlの受入済みrevision`5287c36d7c2a90cc836f4ff2b23b91bffc279f53`から移行した結果である。
保存元と移行の根拠は、前節と[test-suite-runnerの移行記録](../test-suite-runner/wabt-datacount-migration.md)を維持する。

| 用途 | パス | SHA-256 |
| --- | --- | --- |
| manifest | [wabt-commit-20261016/corpus/manifest.json](../../../artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json) | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 比較元baseline | [wabt-commit-20261016/run-baseline.json](../../../artifacts/linear-memory/wabt-commit-20261016/run-baseline.json) | `619961b91dfa83c233864799e7f87323f811e5889c3d6faa7b460812fb72c4f5` |
| 正式run | [review-8.2-20261015T234057463Z/run.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/run.json) | `70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1` |
| 正式comparison | [review-8.2-20261015T234057463Z/comparison.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/comparison.json) | `2b1695fd9e23e8178ee825ba2b7dd02c8750f518d1dd842ced2ffb32a0a7f590` |

8.2の最初の候補は`task-8.2-20261015T233611009Z`に保持している。
独立レビューではfreshビルド後に一部DLLのハッシュ差を観測し、正規のビルド成果物でrun/compareを取り直した。
生成AssemblyInfoがHEADを含むInformationalVersionを持つことを確認したが、それだけでPE差分の原因を断定していない。
旧候補と正式候補の147入力・53,907ケースの詳細、期待値、診断、比較全レコードは同一だった。
正式runの前後でも611ソース・73ビルド成果物・12固定入力・5,821素材のSHA-256が一致した。
根拠は[8.2の独立レビュー](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/review.md)と[independent-audit.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/independent-audit.json)にある。
8.3では同じ611ソースの現在ハッシュ一致と、上表4ファイルのハッシュを再確認した。

### ビルドと直接テスト

8.1はReleaseビルドの警告・エラー0件を確認してから3プロジェクトの全テストを実行し、独立レビューも完了した。
[8.1の実行記録](../../../artifacts/linear-memory/review-8.1-20261015T233017910Z/verification-summary.json)と[レビュー](../../../artifacts/linear-memory/review-8.1-20261015T233017910Z/review.md)を受入証拠として保持する。
同レビューの[29命令監査](../../../artifacts/linear-memory/review-8.1-20261015T233017910Z/instruction-audit.json)では、23スカラー命令とsize/grow/init/drop/copy/fillの宣言、生成記述子、直接dispatch、実ハンドラーが一致した。
[14契約監査](../../../artifacts/linear-memory/review-8.1-20261015T233017910Z/embedded-contract-audit.json)では、MemoryArgument・MemoryAccessSignatureを含む生成器テストの埋込み契約と実ソースが一致した。
8.3でも14ソースのハッシュを照合し、不一致0件だった。

8.2の独立レビューは、次のビルドと全テストを逐次実行してから正式runへ進んだ。
すべて終了0で、全4,084件が成功し、失敗・スキップ・未実行は0件だった。
対応表の直接テスト59メソッドについても、この最新TRXの各パラメーター実行がPassedであることを確認した。

| 検証 | UTC開始～終了 | 件数・結果 | コマンド・終了値 | ログ・TRX |
| --- | --- | --- | --- | --- |
| Releaseビルド | 2026-10-15T23:41:00.0137620Z～23:41:09.0243607Z | 警告0、エラー0 | [build.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/build.result.json) | [stdout](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/build.stdout.log) |
| ランタイム | 2026-10-15T23:41:09.0471587Z～23:41:12.5629475Z | 3,289件成功 | [runtime.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/runtime.result.json) | [TRX](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/runtime-trx/WasmSharp.Tests_net10.0_x64.trx) |
| 生成器 | 2026-10-15T23:41:12.5753749Z～23:41:17.2444607Z | 93件成功 | [generators.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/generators.result.json) | [TRX](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/generators-trx/WasmSharp.Generators.Tests_net10.0_x64.trx) |
| 公式ランナー | 2026-10-15T23:41:17.2552322Z～23:41:21.8964614Z | 702件成功 | [runner.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/runner.result.json) | [TRX](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/runner-trx/WasmSharp.TestSuiteRunner.Tests_net10.0_x64.trx) |

```powershell
$evidence = 'D:\source\repos\WasmSharp2\artifacts\linear-memory\review-8.2-20261015T234057463Z'
dotnet build WasmSharp2.slnx -c Release --warnaserror --disable-build-servers
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/runtime-trx"
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/generators-trx"
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory "$evidence/runner-trx"
```

### 公式全体の実行と比較

正式runはUTC`2026-10-15T23:45:30.8009597Z`から`23:45:34.9372991Z`まで実行され、終了0だった。
同じrunのcompare-runは`23:45:34.9641593Z`から`23:45:39.6485427Z`まで実行され、終了0だった。
正確な引数・作業ディレクトリ・UTC・終了値は[run.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/run.result.json)と[compare.result.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/compare.result.json)、標準出力は[run.stdout.log](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/run.stdout.log)と[compare.stdout.log](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/compare.stdout.log)に保存されている。
両コマンドの標準エラーは空だった。

```powershell
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe run --manifest artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json --output artifacts/linear-memory/review-8.2-20261015T234057463Z/run.json
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe compare-run --baseline artifacts/linear-memory/wabt-commit-20261016/run-baseline.json --current artifacts/linear-memory/review-8.2-20261015T234057463Z/run.json --output artifacts/linear-memory/review-8.2-20261015T234057463Z/comparison.json
```

| 項目 | 件数・結果 |
| --- | --- |
| 入力・一意な公式ケース | 147入力、53,907ケース |
| passed | 23,050 |
| runtime_unsupported | 1,457 |
| blocked | 28,323 |
| out_of_scope | 1,077 |
| failed・runner_error | 各0 |
| 回帰・欠落・追加・重複・未比較 | 各0 |
| 以前のpassed | 全16,924件がpassedを維持 |
| 比較条件・出典・入力素材の差 | 各0 |

8.3の[機械監査](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/acceptance-audit.json)は、全53,907ケースの識別子・順序・command種別・期待値・期待診断を公式JSONから照合し、比較の旧新レコードも実際のbaseline/runと一致することを確認した。
passedの否定assertion2,553件では、処理段階・公開例外型・診断先頭を照合した。
内訳はassert_malformed729件、assert_invalid1,301件、assert_unlinkable78件、assert_uninstantiable17件、assert_trap417件、assert_exhaustion11件である。
assert_unlinkableのReasonも既存のリンク不成立分類と一致した。
値の成立は既存ランナーの判定とその全テストに基づき、8.3で別の値判定器や期待値の正規化は追加していない。

### 54項目の対応

以下のcaseIDは`input_path#command_index`であり、0起点のcommand番号である。
掲載した公式ケースはすべて正式runでpassedだった。
直接テストはファイル名からクラス名を推測せず、実際のクラス・メソッドと最新TRXを対応付けた。
表の成功件数はTRXの実行行数である。
全パラメーター・テストID・公式command・観測診断は[8.3時点のcoverage-matrix.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/coverage-matrix.json)に保存した。
下表の要件8.6は8.4の保存後に更新し、54項目すべての最終対応は[coverage-matrix-final.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/coverage-matrix-final.json)に保存した。

要件4.3の実際のメモリ割当失敗は再現していない。
設計指定のコード確認として、[Grow](../../../src/WasmSharp/Execution/Interpreter.Memory.cs#L116)がTryGrow呼出しだけをOutOfMemoryException捕捉で囲み-1へ変換することと、[TryGrow](../../../src/WasmSharp/WasmMemory.cs#L119)が全追加領域の割当後にページ表を確定することを確認した。
4GiBの確保や割当失敗を強制するための差し替え機構は導入していない。
要件8.6の直接テストは既存の保存判定の確認であり、今回の受入runの保存実績には数えない。

| 要件 | 確認した内容 | 公式ケースまたは直接テスト | 状態 |
| --- | --- | --- | --- |
| 1.1 | 全23スカラー命令のu32 align/offsetと元位置を保持する | `align.wast#0`<br>`align.wast#22`<br>`binary-leb128.wast#40`<br>`binary-leb128.wast#65`<br>[ModuleDecoder_DecodeTests.メモリ命令にu32の即値がある_全命令のalignとoffsetと元位置を意味検査せず保持する](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_DecodeMemoryTests.cs#L13)（3件成功） | 照合済み |
| 1.2 | size/grow/init/copy/fill/dropの即値を読み不正予約byteと途切れをDecodeで拒否する | `binary.wast#106`<br>`binary.wast#107`<br>`binary.wast#114`<br>`binary.wast#115`<br>[WasmModule_DecodeTests.メモリ命令の予約バイトが不正か欠落する_両Decode入口が構文診断と位置を返す](../../../tests/WasmSharp.Tests/WasmModule_DecodeMemoryTests.cs#L26)（17件成功）<br>[WasmModule_DecodeTests.データ参照命令の即値が不正か欠落する_両Decode入口が構文診断と元位置を返す](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataInstructionTests.cs#L20)（11件成功）<br>[WasmFunction_InvokeTests.メモリサイズ取得と拡張を公開4段階で呼び出す_現在サイズと拡張前サイズを返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L15)（6件成功）<br>[WasmFunction_InvokeTests.メモリコピーの範囲が重なる_両方向と同一位置で元内容をコピーして結果を返さない](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L12)（3件成功）<br>[WasmFunction_InvokeTests.メモリの指定範囲を値で埋める_ページ境界をまたいで下位8ビットだけを書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L46)（3件成功） | 照合済み |
| 1.3 | flags0/1/2とbyte列・offset式・メモリ参照を保持する | `data.wast#0`<br>`binary.wast#47`<br>`binary.wast#49`<br>`memory_init.wast#0`<br>[ModuleDecoder_ReadDataTests.アクティブとパッシブの宣言がある_バイト列と初期化先と位置を独立して保持する](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadDataTests.cs#L9)（1件成功）<br>[WasmModule_DecodeTests.データセクションを読み元入力を変更する_両入口で初期バイト列とオフセット式を独立して保持する](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataTests.cs#L13)（4件成功） | 照合済み |
| 1.4 | 不正flag/LEB/長さ/式終端/section順序・重複をDecodeで拒否する | `binary-leb128.wast#27`<br>`binary-leb128.wast#52`<br>`binary.wast#171`<br>`binary.wast#172`<br>[WasmModule_DecodeTests.データとデータ数の構文や順序が不正である_両入口でDecode診断と位置を返す](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataTests.cs#L133)（15件成功） | 照合済み |
| 1.5 | DataCount不一致・データindex使用時のDataCount欠落をDecodeで拒否する | `binary.wast#127`<br>`binary.wast#128`<br>`binary.wast#129`<br>`binary.wast#130`<br>[WasmModule_DecodeTests.データ数が省略時の0件または宣言件数と一致しない_両入口で読了後にDecode失敗になる](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataTests.cs#L85)（4件成功）<br>[WasmModule_DecodeTests.到達性によらずコードがデータインデックスを使いデータ数宣言がない_両Decode入口が欠落を拒否する](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataInstructionTests.cs#L59)（4件成功） | 照合済み |
| 1.6 | 省略・空data・不要でも整合するDataCountを受理する | `binary.wast#47`<br>`binary.wast#49`<br>`binary.wast#50`<br>`binary.wast#51`<br>`data.wast#0`<br>[WasmModule_DecodeTests.データとデータ数セクションを省略するか整合する内容を指定する_メモリなしでも両入口で生成できる](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataTests.cs#L60)（6件成功） | 照合済み |
| 1.7 | buffer/Stream元入力の変更後も保持dataと命令の意味を維持する | [WasmModule_DecodeTests.データセクションを読み元入力を変更する_両入口で初期バイト列とオフセット式を独立して保持する](../../../tests/WasmSharp.Tests/WasmModule_DecodeDataTests.cs#L13)（4件成功）<br>[ModuleDecoder_ReadDataTests.アクティブとパッシブの宣言がある_バイト列と初期化先と位置を独立して保持する](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_ReadDataTests.cs#L9)（1件成功）<br>[WasmFunction_InvokeTests.ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L181)（14件成功） | 照合済み |
| 2.1 | メモリを参照する全対象命令でmemory0を要求する | `memory.wast#17`<br>`memory.wast#22`<br>`memory_init.wast#136`<br>`memory_copy.wast#4119`<br>`memory_fill.wast#27`<br>[WasmModule_ValidateTests.メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L51)（42件成功）<br>[WasmModule_ValidateTests.データ参照命令に参照や型の違反がある_到達性によらずメモリとデータと型の順でValidateが拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataInstructionTests.cs#L23)（14件成功） | 照合済み |
| 2.2 | load/storeのi32アドレス・値型・結果型と無結果を検証する | `load.wast#51`<br>`load.wast#78`<br>`store.wast#17`<br>`store.wast#25`<br>`store.wast#50`<br>`store.wast#67`<br>[WasmModule_ValidateTests.メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L51)（42件成功）<br>[WasmFunction_InvokeTests.ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L181)（14件成功）<br>[WasmFunction_InvokeTests.ストア命令を公開4段階で呼び出す_定義とインポートのメモリへ指定幅だけ書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L236)（9件成功） | 照合済み |
| 2.3 | 全load/storeの自然幅超過alignと指数33/u32最大をValidateで拒否する | `align.wast#69`<br>`align.wast#105`<br>[WasmModule_ValidateTests.メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L51)（42件成功） | 照合済み |
| 2.4 | size/growの引数結果とinit/copy/fillの3個のi32・無結果を検証する | `memory_size.wast#40`<br>`memory_size.wast#41`<br>`memory_grow.wast#89`<br>`memory_grow.wast#95`<br>`memory_init.wast#158`<br>`memory_init.wast#173`<br>`memory_copy.wast#4120`<br>`memory_copy.wast#4135`<br>`memory_fill.wast#28`<br>`memory_fill.wast#43`<br>[WasmModule_ValidateTests.メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L51)（42件成功）<br>[WasmModule_ValidateTests.データ参照命令に参照や型の違反がある_到達性によらずメモリとデータと型の順でValidateが拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataInstructionTests.cs#L23)（14件成功） | 照合済み |
| 2.5 | data indexの存在を要求しactive/passiveの両方を参照可能とする | `memory_init.wast#128`<br>`memory_init.wast#129`<br>`memory_init.wast#134`<br>`memory_init.wast#137`<br>`memory_init.wast#138`<br>`memory_init.wast#139`<br>[WasmModule_ValidateTests.データ参照命令に参照や型の違反がある_到達性によらずメモリとデータと型の順でValidateが拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataInstructionTests.cs#L23)（14件成功）<br>[WasmFunction_InvokeTests.アクティブデータは初期化後も同じインデックスで参照できる_空データとして扱い繰り返し破棄できる](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L198)（1件成功） | 照合済み |
| 2.6 | メモリなしpassive/data.dropを受理し空引数・空結果を検証する | [WasmFunction_InvokeTests.メモリのないmoduleでパッシブデータを繰り返し破棄する_引数も結果もなく成功する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L9)（1件成功）<br>[WasmModule_ValidateTests.データ参照命令に参照や型の違反がある_到達性によらずメモリとデータと型の順でValidateが拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataInstructionTests.cs#L23)（14件成功） | 照合済み |
| 2.7 | activeのmemory参照と単一i32定数式、import不変globalだけを受理する | `data.wast#5`<br>`data.wast#9`<br>`data.wast#10`<br>`data.wast#41`<br>`data.wast#47`<br>`data.wast#49`<br>`data.wast#60`<br>[WasmModule_ValidateTests.データのメモリ参照や定数式が不正である_Decodeは成功してValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataTests.cs#L23)（7件成功）<br>[ModuleValidator_ValidateTests.アクティブデータのメモリ参照とI32定数式が有効_書き込み範囲を調べず検証済みにする](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateDataTests.cs#L119)（4件成功） | 照合済み |
| 2.8 | 到達不能のスタック多相性と参照・align検査を両立し全体成功時だけ検証を確定する | [WasmModule_ValidateTests.メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L51)（42件成功）<br>[WasmModule_ValidateTests.到達不能なメモリ命令に具体型と参照とalignの違反がない_スタック多相性でValidateに成功する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L94)（8件成功）<br>[WasmModule_ValidateTests.データ参照が有効で具体型の違反がない_非最短LEBと到達不能スタックを受理する](../../../tests/WasmSharp.Tests/WasmModule_ValidateDataInstructionTests.cs#L72)（4件成功）<br>[WasmModule_ValidateTests.後半関数のメモリ命令が不正_検証済みの命令を部分公開せず再試行でも拒否する](../../../tests/WasmSharp.Tests/WasmModule_ValidateMemoryTests.cs#L117)（1件成功） | 照合済み |
| 3.1 | i32アドレスをu32としoffsetとの33bit以上の和を折り返さない | `address.wast#82`<br>`address.wast#87`<br>`address.wast#206`<br>`address.wast#213`<br>`memory_trap.wast#15`<br>[Interpreter_LoadTests.読み出し範囲がメモリ外または33ビット加算で範囲外になる_折り返さず元の関数と命令位置を持つtrapを返す](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L239)（8件成功）<br>[Interpreter_StoreTests.書き込み範囲がメモリ外または33ビット加算で範囲外になる_全メモリを変更せず元の位置を持つtrapを返す](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L203)（7件成功） | 照合済み |
| 3.2 | 整数と浮動小数点の全幅をlittle endianで読み書きする | `endianness.wast#1`<br>`endianness.wast#41`<br>`endianness.wast#68`<br>[WasmFunction_InvokeTests.ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L181)（14件成功）<br>[WasmFunction_InvokeTests.ストア命令を公開4段階で呼び出す_定義とインポートのメモリへ指定幅だけ書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L236)（9件成功） | 照合済み |
| 3.3 | 全10狭幅loadで符号拡張・ゼロ拡張を行う | `endianness.wast#1`<br>`endianness.wast#5`<br>`endianness.wast#13`<br>`endianness.wast#17`<br>`endianness.wast#21`<br>`endianness.wast#25`<br>[Interpreter_LoadTests.メモリ末尾の狭い整数の最上位ビットが1_指定した幅だけ読み出して符号拡張またはゼロ拡張する](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L85)（10件成功）<br>[Interpreter_LoadTests.符号付きの狭い整数の最上位ビットが0_正の整数として拡張する](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L125)（5件成功） | 照合済み |
| 3.4 | 全5狭幅storeで下位8/16/32bitだけを書き前後を保つ | `endianness.wast#41`<br>`endianness.wast#49`<br>`endianness.wast#53`<br>[Interpreter_StoreTests.値型より狭い幅へ書き込む_指定した下位ビットだけを書き込み前後のバイトを保持する](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L86)（5件成功） | 照合済み |
| 3.5 | f32/f64の正負0・NaNの符号とpayloadを全bit保持する | `float_memory.wast#1`<br>`float_memory.wast#7`<br>`float_memory.wast#16`<br>`float_memory.wast#22`<br>`float_memory.wast#61`<br>`float_memory.wast#67`<br>`float_memory.wast#76`<br>`float_memory.wast#82`<br>[Interpreter_LoadTests.浮動小数点の通常値と正負の0とNaN_リトルエンディアンの全ビットを保持する](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L194)（14件成功）<br>[Interpreter_StoreTests.浮動小数点の通常値と正負の0とNaN_全ビットをリトルエンディアンで書き込む](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L158)（14件成功） | 照合済み |
| 3.6 | 範囲内非整列とページ境界跨ぎで全byteを読み書きする | `align.wast#107`<br>`align.wast#152`<br>[Interpreter_LoadTests.I64をページ境界またはメモリ末尾から読み出す_全8バイトを読み出す](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L44)（2件成功）<br>[Interpreter_StoreTests.I32をページ境界またはメモリ末尾へ書き込む_全4バイトを変更する](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L50)（2件成功）<br>[Interpreter_LoadTests.浮動小数点の通常値と正負の0とNaN_リトルエンディアンの全ビットを保持する](../../../tests/WasmSharp.Tests/Execution/Interpreter_LoadTests.cs#L194)（14件成功）<br>[Interpreter_StoreTests.浮動小数点の通常値と正負の0とNaN_全ビットをリトルエンディアンで書き込む](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L158)（14件成功） | 照合済み |
| 3.7 | 全23命令で範囲外trapと失敗storeの非変更を確認する | `memory_trap.wast#15`<br>`memory_trap.wast#23`<br>`memory_trap.wast#39`<br>`memory_trap.wast#47`<br>`memory_trap.wast#63`<br>`memory_trap.wast#65`<br>`memory_trap.wast#69`<br>`memory_trap.wast#71`<br>`memory_trap.wast#75`<br>`memory_trap.wast#83`<br>`memory_trap.wast#91`<br>`memory_trap.wast#107`<br>`memory_trap.wast#115`<br>`memory_trap.wast#131`<br>`memory_trap.wast#133`<br>`memory_trap.wast#135`<br>`memory_trap.wast#139`<br>`memory_trap.wast#143`<br>`memory_trap.wast#145`<br>`memory_trap.wast#147`<br>`memory_trap.wast#151`<br>`memory_trap.wast#155`<br>`memory_trap.wast#163`<br>`align.wast#154`<br>`align.wast#155`<br>[Interpreter_StoreTests.書き込み範囲がメモリ外または33ビット加算で範囲外になる_全メモリを変更せず元の位置を持つtrapを返す](../../../tests/WasmSharp.Tests/Execution/Interpreter_StoreTests.cs#L203)（7件成功） | 照合済み |
| 4.1 | 現在ページ数を65536byte単位のi32で返す | `memory_size.wast#1`<br>`memory_size.wast#3`<br>`memory_size.wast#5`<br>`memory_size.wast#7`<br>`memory_size.wast#39`<br>[WasmFunction_InvokeTests.メモリサイズ取得と拡張を公開4段階で呼び出す_現在サイズと拡張前サイズを返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L15)（6件成功） | 照合済み |
| 4.2 | grow成功時に旧ページ数を返し既存内容と追加領域ゼロを保つ | `memory_grow.wast#6`<br>`memory_grow.wast#13`<br>`memory_grow.wast#15`<br>`memory_grow.wast#18`<br>`memory_grow.wast#41`<br>`memory_grow.wast#42`<br>`memory_grow.wast#44`<br>`memory_grow.wast#46`<br>`memory_grow.wast#48`<br>`memory_grow.wast#50`<br>[WasmFunction_InvokeTests.メモリサイズ取得と拡張を公開4段階で呼び出す_現在サイズと拡張前サイズを返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L15)（6件成功）<br>[WasmMemory_TryGrowTests.増大する_既存バイトを保持し追加ページをゼロ初期化する](../../../tests/WasmSharp.Tests/WasmMemory_TryGrowTests.cs#L6)（1件成功） | 照合済み |
| 4.3 | 実体のmax/Core上限超過で-1と不変状態。実OOMは設計指定のコード確認で補完する | `memory_grow.wast#27`<br>`memory_grow.wast#28`<br>`memory_grow.wast#29`<br>`memory_grow.wast#37`<br>`memory_grow.wast#38`<br>[WasmFunction_InvokeTests.符号なし拡張量が実体の最大値かCore上限を超える_失敗値を返しサイズと内容を保持する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L64)（4件成功） | 照合済み |
| 4.4 | grow0で現在ページ数を返しサイズと全既存byteを維持する | `memory_grow.wast#22`<br>`memory_grow.wast#24`<br>`memory_grow.wast#31`<br>`memory_grow.wast#36`<br>[WasmFunction_InvokeTests.メモリサイズ取得と拡張を公開4段階で呼び出す_現在サイズと拡張前サイズを返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L15)（6件成功） | 照合済み |
| 4.5 | ホストと別instanceのgrow/writeを後続memory命令で観測する | [WasmFunction_InvokeTests.ホストと複数instanceが同じメモリを拡張して書き込む_後続命令から更新後のサイズと内容を観測する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L100)（1件成功） | 照合済み |
| 5.1 | 同じmoduleの別instanceでデータ破棄状態を独立させる | [WasmFunction_InvokeTests.同じmoduleの一方のinstanceだけでデータを破棄する_他方の内容を保ち破棄済みデータは空として扱う](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L86)（1件成功） | 照合済み |
| 5.2 | passiveを初期化時に書かずmemory.initで再利用する | `bulk.wast#47`<br>`bulk.wast#48`<br>`bulk.wast#49`<br>`bulk.wast#50`<br>[WasmModule_InstantiateTests.パッシブデータを定義する_メモリの有無にかかわらず内容を保持して初期化時には書き込まない](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L183)（2件成功）<br>[WasmFunction_InvokeTests.パッシブデータを部分コピーして再利用する_指定範囲だけを書き込み元データを保持する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L37)（2件成功） | 照合済み |
| 5.3 | 宣言順にoffsetを評価し全体を書いたactiveを破棄する | `linking.wast#109`<br>`linking.wast#112`<br>`linking.wast#113`<br>[WasmModule_InstantiateTests.公開Decodeしたデータを共有メモリへ初期化する_成功時だけStartへ進み失敗時は先行変更と位置を保持する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L14)（2件成功）<br>[WasmModule_InstantiateTests.インポートした不変globalをオフセットに使う_拡張済み共有メモリの現在範囲に書き込む](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L275)（1件成功）<br>[WasmFunction_InvokeTests.アクティブデータは初期化後も同じインデックスで参照できる_空データとして扱い繰り返し破棄できる](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L198)（1件成功） | 照合済み |
| 5.4 | 重複領域には後の宣言の内容を残す | `linking.wast#112`<br>`linking.wast#113`<br>[WasmModule_InstantiateTests.アクティブデータが重なる_宣言順に書き込み成功した実体を破棄する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L144)（1件成功） | 照合済み |
| 5.5 | 全active初期化と破棄の後にstartを実行する | [WasmModule_InstantiateTests.全アクティブデータの初期化が成功する_Startから初期化済みメモリと破棄済み実体を参照できる](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L334)（2件成功）<br>[WasmModule_InstantiateTests.開始関数が初期化済みのアクティブデータを参照する_破棄済みとして扱い空コピーだけ成功する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataInstructionTests.cs#L74)（2件成功） | 照合済み |
| 5.6 | 範囲外activeの部分書込み・後続初期化・startを行わない | `data.wast#27`<br>`data.wast#28`<br>`linking.wast#110`<br>`linking.wast#122`<br>[WasmModule_InstantiateTests.公開Decodeしたデータを共有メモリへ初期化する_成功時だけStartへ進み失敗時は先行変更と位置を保持する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L14)（2件成功）<br>[WasmModule_InstantiateTests.アクティブデータが現在メモリの範囲を超える_先行書き込みだけ残して後続とStartを中断する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L424)（5件成功） | 照合済み |
| 5.7 | 後続初期化やstart失敗後も完了済み変更と保存参照を保持する | `linking.wast#122`<br>`linking.wast#123`<br>`linking.wast#124`<br>[WasmModule_InstantiateTests.初期化とメモリ書き込み後のStartが失敗する_元の失敗と保存参照から初期化済みメモリを使える](../../../tests/WasmSharp.Tests/WasmModule_InstantiateMemoryFailureIntegrationTests.cs#L11)（2件成功）<br>[WasmModule_InstantiateTests.アクティブデータが現在メモリの範囲を超える_先行書き込みだけ残して後続とStartを中断する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L424)（5件成功） | 照合済み |
| 5.8 | 空activeの末尾offsetは成功し末尾超過はtrapとする | `data.wast#1`<br>`data.wast#28`<br>[WasmModule_InstantiateTests.空のアクティブデータの開始位置が現在メモリの末尾にある_初期化に成功してインデックスを保持する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L500)（2件成功）<br>[WasmModule_InstantiateTests.アクティブデータが現在メモリの範囲を超える_先行書き込みだけ残して後続とStartを中断する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L424)（5件成功） | 照合済み |
| 6.1 | memory.initの指定範囲をコピーし元データを保持する | `bulk.wast#48`<br>`bulk.wast#49`<br>`bulk.wast#50`<br>`bulk.wast#52`<br>[WasmFunction_InvokeTests.パッシブデータを部分コピーして再利用する_指定範囲だけを書き込み元データを保持する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L37)（2件成功） | 照合済み |
| 6.2 | data.dropは繰り返せて対象instanceだけを空にする | `bulk.wast#62`<br>`bulk.wast#63`<br>`bulk.wast#64`<br>`bulk.wast#67`<br>`bulk.wast#68`<br>[WasmFunction_InvokeTests.メモリのないmoduleでパッシブデータを繰り返し破棄する_引数も結果もなく成功する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L9)（1件成功）<br>[WasmFunction_InvokeTests.同じmoduleの一方のinstanceだけでデータを破棄する_他方の内容を保ち破棄済みデータは空として扱う](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L86)（1件成功） | 照合済み |
| 6.3 | 破棄後はsource=length=0かつdestination範囲内だけ成功する | `bulk.wast#64`<br>`bulk.wast#65`<br>`bulk.wast#68`<br>`bulk.wast#69`<br>[WasmFunction_InvokeTests.memory_initの全範囲と破棄状態を検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L133)（14件成功） | 照合済み |
| 6.4 | memory.copyは両方向の重なりでコピー前の内容を使う | `bulk.wast#26`<br>`bulk.wast#27`<br>`bulk.wast#28`<br>`bulk.wast#29`<br>`bulk.wast#30`<br>`bulk.wast#33`<br>`bulk.wast#34`<br>`bulk.wast#35`<br>`memory_copy.wast#1`<br>`memory_copy.wast#4`<br>[WasmFunction_InvokeTests.メモリコピーの範囲が重なる_両方向と同一位置で元内容をコピーして結果を返さない](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L12)（3件成功） | 照合済み |
| 6.5 | memory.fillは値の下位8ビットで指定範囲だけを埋める | `bulk.wast#9`<br>`bulk.wast#10`<br>`bulk.wast#11`<br>`memory_fill.wast#2`<br>`memory_fill.wast#3`<br>[WasmFunction_InvokeTests.メモリの指定範囲を値で埋める_ページ境界をまたいで下位8ビットだけを書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L46)（3件成功） | 照合済み |
| 6.6 | 全開始位置と長さをu32で解釈し折返しのない全範囲を検査する | `bulk.wast#13`<br>`bulk.wast#53`<br>[WasmFunction_InvokeTests.memory_initの全範囲と破棄状態を検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L133)（14件成功）<br>[WasmFunction_InvokeTests.メモリ一括操作の全範囲を符号なしで検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L92)（20件成功） | 照合済み |
| 6.7 | init/copy/fillが失敗した命令では全メモリを変更しない | `bulk.wast#13`<br>`bulk.wast#14`<br>`bulk.wast#15`<br>`bulk.wast#53`<br>`bulk.wast#54`<br>`bulk.wast#55`<br>[WasmFunction_InvokeTests.memory_initの全範囲と破棄状態を検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L133)（14件成功）<br>[WasmFunction_InvokeTests.メモリ一括操作の全範囲を符号なしで検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L92)（20件成功） | 照合済み |
| 6.8 | 長さ0でも各開始位置の末尾以内を要求する | `bulk.wast#16`<br>`bulk.wast#17`<br>`bulk.wast#43`<br>`bulk.wast#44`<br>`bulk.wast#45`<br>`bulk.wast#46`<br>`bulk.wast#56`<br>`bulk.wast#57`<br>`bulk.wast#58`<br>`bulk.wast#59`<br>[WasmFunction_InvokeTests.memory_initの全範囲と破棄状態を検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする](../../../tests/WasmSharp.Tests/WasmFunction_InvokeDataTests.cs#L133)（14件成功）<br>[WasmFunction_InvokeTests.空メモリに長さ0の一括操作を行う_各開始点0だけが成功する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeBulkMemoryTests.cs#L155)（2件成功） | 照合済み |
| 7.1 | 定義とimportに同じ規則を適用しexport・再export・ホストが同じ実体を観測する | `linking.wast#101`<br>`linking.wast#102`<br>`linking.wast#103`<br>`linking.wast#105`<br>`linking.wast#106`<br>`imports.wast#118`<br>`imports.wast#123`<br>[WasmFunction_InvokeTests.ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L181)（14件成功）<br>[WasmFunction_InvokeTests.ストア命令を公開4段階で呼び出す_定義とインポートのメモリへ指定幅だけ書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L236)（9件成功）<br>[WasmInstance_GetMemoryTests.別名と再exportから取得する_更新と増大を同じmemoryで共有する](../../../tests/WasmSharp.Tests/WasmInstance_GetMemoryTests.cs#L8)（1件成功） | 照合済み |
| 7.2 | Invoke/Instantiate、命令とデータ位置、関数indexとMemoryOutOfBoundsを区別する | `traps.wast#22`<br>`data.wast#27`<br>`linking.wast#110`<br>[WasmFunction_InvokeTests.メモリ命令の実効範囲が領域を超える_Invokeの元命令位置でtrapし内容を保持する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L286)（3件成功）<br>[WasmModule_InstantiateTests.開始関数のメモリ一括操作が範囲外になる_共有メモリを変更せずInstantiateと元命令位置を持つtrapになる](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataInstructionTests.cs#L11)（2件成功）<br>[WasmModule_InstantiateTests.初期化とメモリ書き込み後のStartが失敗する_元の失敗と保存参照から初期化済みメモリを使える](../../../tests/WasmSharp.Tests/WasmModule_InstantiateMemoryFailureIntegrationTests.cs#L11)（2件成功）<br>[WasmModule_InstantiateTests.公開Decodeしたデータを共有メモリへ初期化する_成功時だけStartへ進み失敗時は先行変更と位置を保持する](../../../tests/WasmSharp.Tests/WasmModule_InstantiateDataTests.cs#L14)（2件成功） | 照合済み |
| 7.3 | 失敗分類、growの-1、公開API引数例外、host例外と深さ上限を維持する | `binary.wast#129`<br>`memory_init.wast#128`<br>`linking.wast#96`<br>`traps.wast#22`<br>`memory_grow.wast#27`<br>[WasmFunction_InvokeTests.符号なし拡張量が実体の最大値かCore上限を超える_失敗値を返しサイズと内容を保持する](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L64)（4件成功）<br>[WasmFunction_InvokeTests.共有メモリを拡張して同期再入する_正常終了とtrapとhost例外と深さ上限の後も外側と次の実行を保つ](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryIntegrationTests.cs#L96)（4件成功）<br>[WasmMemory_ReadTests.範囲外から読み出す_転送先を一部も変更せず拒否する](../../../tests/WasmSharp.Tests/WasmMemory_ReadTests.cs#L32)（4件成功）<br>[WasmMemory_WriteTests.範囲外へ書き込む_部分変更を残さず拒否する](../../../tests/WasmSharp.Tests/WasmMemory_WriteTests.cs#L33)（4件成功）<br>[ExecutionBoundary_InvokeTests.関数入口で保持上限を超える_再分類せず伝播し現在のコンテキストを解除する](../../../tests/WasmSharp.Tests/Execution/ExecutionBoundary_InvokeTests.cs#L66)（1件成功） | 照合済み |
| 7.4 | 再exportした定義関数は所属instanceを使い同期再入の状態・上限・例外を維持する | [WasmFunction_InvokeTests.定義関数を再exportして別instanceから呼ぶ_所属元のメモリとデータの内容と破棄状態を使う](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryIntegrationTests.cs#L9)（1件成功）<br>[WasmFunction_InvokeTests.共有メモリを拡張して同期再入する_正常終了とtrapとhost例外と深さ上限の後も外側と次の実行を保つ](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryIntegrationTests.cs#L96)（4件成功） | 照合済み |
| 7.5 | 未対応機能の原因と未確認範囲を保ち生成・後続処理を成功させない | [WasmModule_InspectImportsTests.メモリ命令とデータに後続仕様の未対応が併存する_生成を拒否してimport調査は本体とデータを未確認に保つ](../../../tests/WasmSharp.Tests/WasmModule_InspectImportsTests.cs#L386)（3件成功）<br>[root-cause-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/root-cause-audit.json) | 照合済み |
| 8.1 | 固定全147入力の期待値・段階・分類・否定診断先頭を照合する | `binary.wast#129`<br>`memory_init.wast#128`<br>`traps.wast#22`<br>`linking.wast#110`<br>[acceptance-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/acceptance-audit.json)<br>[official-related-cases.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/official-related-cases.json) | 照合済み |
| 8.2 | 全54項目、全23スカラー命令、境界・一括操作・初期化・共有へ対応付ける | `bulk.wast#26`<br>`bulk.wast#43`<br>`memory_init.wast#128`<br>[WasmFunction_InvokeTests.ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L181)（14件成功）<br>[WasmFunction_InvokeTests.ストア命令を公開4段階で呼び出す_定義とインポートのメモリへ指定幅だけ書き込む](../../../tests/WasmSharp.Tests/WasmFunction_InvokeMemoryTests.cs#L236)（9件成功）<br>[coverage-matrix.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/coverage-matrix.json) | 照合済み |
| 8.3 | 比較元の全passed16924件を維持し欠落・未比較・回帰を0件とする | `i32.wast#1`<br>[acceptance-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/acceptance-audit.json)<br>[comparison.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/comparison.json) | 照合済み |
| 8.4 | failed/runner_error/回帰と本仕様の未対応起因が残らず固定条件を維持する | `memory_init.wast#128`<br>`memory_init.wast#136`<br>[acceptance-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/acceptance-audit.json)<br>[root-cause-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/root-cause-audit.json) | 照合済み |
| 8.5 | 後続のtables-referencesとsimdへの依存を分類と起因で保持する | [WasmModule_InspectImportsTests.メモリ命令とデータに後続仕様の未対応が併存する_生成を拒否してimport調査は本体とデータを未確認に保つ](../../../tests/WasmSharp.Tests/WasmModule_InspectImportsTests.cs#L386)（3件成功）<br>[root-cause-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/root-cause-audit.json) | 照合済み |
| 8.6 | 比較済みの同じ正式runを明示保存し、全バイト・ケース・分類・metadataの一致と旧比較元の不変を確認する | [baseline-save.result.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save.result.json)<br>[baseline-save-audit.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save-audit.json)<br>[run-baseline.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/run-baseline.json) | 保存・照合済み |


### 関連する公式入力の洗い出し

設計が挙げる23入力の全6,757commandを[official-related-cases.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/official-related-cases.json)へ保存した。
各commandの公式JSONと正式runの結果を並べ、本仕様の直接の受入証拠は前表で特定した。
これらの入力にはテーブル等を扱うcommandも含まれるため、ファイル全体をメモリ専用と扱わない。
全147入力に残る未対応とblockedは、次節の監査で別に全件追跡した。

| 入力 | 全command | passed | runtime_unsupported | blocked | out_of_scope |
| --- | --- | --- | --- | --- | --- |
| `address.wast` | 260 | 259 | 0 | 0 | 1 |
| `align.wast` | 156 | 110 | 0 | 0 | 46 |
| `binary-leb128.wast` | 83 | 80 | 3 | 0 | 0 |
| `binary.wast` | 177 | 164 | 13 | 0 | 0 |
| `bulk.wast` | 117 | 72 | 6 | 39 | 0 |
| `custom.wast` | 11 | 10 | 1 | 0 | 0 |
| `data.wast` | 61 | 60 | 1 | 0 | 0 |
| `endianness.wast` | 69 | 69 | 0 | 0 | 0 |
| `float_memory.wast` | 90 | 90 | 0 | 0 | 0 |
| `imports.wast` | 183 | 152 | 3 | 12 | 16 |
| `linking.wast` | 132 | 68 | 12 | 52 | 0 |
| `load.wast` | 97 | 45 | 2 | 37 | 13 |
| `memory.wast` | 79 | 73 | 0 | 0 | 6 |
| `memory_copy.wast` | 4,450 | 4,450 | 0 | 0 | 0 |
| `memory_fill.wast` | 100 | 100 | 0 | 0 | 0 |
| `memory_grow.wast` | 96 | 58 | 1 | 37 | 0 |
| `memory_init.wast` | 240 | 240 | 0 | 0 | 0 |
| `memory_redundancy.wast` | 8 | 8 | 0 | 0 | 0 |
| `memory_size.wast` | 42 | 42 | 0 | 0 | 0 |
| `memory_trap.wast` | 182 | 182 | 0 | 0 | 0 |
| `start.wast` | 20 | 19 | 0 | 0 | 1 |
| `store.wast` | 68 | 59 | 2 | 0 | 7 |
| `traps.wast` | 36 | 36 | 0 | 0 | 0 |


### 残る未対応の起因

[root-cause-audit.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/root-cause-audit.json)で、全runtime_unsupported1,457件とblocked28,323件を正式runから再集計した。
所有境界は本仕様の「対象範囲と隣接仕様」「責務境界」に従い、[tables-referencesの範囲](../tables-references/brief.md#範囲)と[simdの範囲](../simd/brief.md#範囲)も照合した。

| 原因を担当する仕様 | 未対応feature数 | runtime_unsupported | 同じ起因によるblocked |
| --- | --- | --- | --- |
| tables-references | 13 | 379 | 4,074 |
| simd | 224 | 1,078 | 24,249 |
| 所有不明・linear-memory自身 | 0 | 0 | 0 |

tables-referencesの379件は、section.element308、call_indirect19、ref.null19、ref.func5、ref.is_null3、elem.drop1、table.get7、table.set4、table.init1、table.copy1、table.grow3、table.size3、table.fill5である。
SIMDの1,078件は224種類の命令featureで、命令宣言のprefixはすべて0xFDだった。
全237featureの件数・宣言位置と各caseIDは監査JSONに保持した。

未対応1,457件はすべてDecode段階だった。
命令診断1,149件は[InstructionSet](../../../src/WasmSharp/Instructions/InstructionSet.cs)の未対応宣言と、元Wasmの診断byte_offsetにあるopcodeまたはprefix後のu32を照合して一致した。
section.element308件も実バイトがセクションID9であることを照合し、[ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs#L209)の未対応分岐と一致した。
宣言欠落、対応済み命令の未対応誤分類、バイナリ位置の不一致、linear-memory命令の未対応は0件だった。

blockedは全28,323件についてcause.directを再帰追跡し、終端集合とcause.originsが一致した。
参照先は28,317件がruntime_unsupported、6件がblockedであり、再帰追跡の終端384ケースはすべて上記の未対応だった。
参照先欠落・cause欠落・循環・異なる入力または後続commandへの参照・保存originとの不一致・所有不明は0件だった。
代表例は`block.wast#1→#0(section.element)`と`simd/simd_address.wast#1→#0(v128.load)`である。
blockedを経由する6件も、`linking.wast#33～#37→#32(register)→#31(ref.null)`と`simd/simd_linking.wast#2→#1(register)→#0(v128.const)`まで追跡した。

この結果と対応表により、本仕様の対象挙動と比較の受入条件を満たすことを確認した。
未対応を無視した成功への変更や、期待値・分類・素材・profileの変更で通したケースはない。
runtime_unsupportedとblockedは未確認範囲を残した結果であり、各入力全体が仕様上有効と証明されたことも、Core 2.0全件が合格したことも意味しない。

### 8.3の監査と次の保存工程

8.3の再実行用監査は[audit-acceptance.py](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/audit-acceptance.py)に保存した。
実コマンド・UTC・終了値・stdout/stderrは[audit-first.result.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/audit-first.result.json)と同名ログ、元のビルド・テスト・run/compareの記録は[source-commands.json](../../../artifacts/linear-memory/task-8.3-20261015T235054739Z/source-commands.json)にある。
監査は終了0だった。
監査コマンドの最初の起動はPowerShellの外部`-File`へ配列を渡す形が不正で終了2となったため、同じシェル内の引数配列に修正した。
失敗ログは`audit-initial.*`に保持し、監査ロジックの成功と区別した。

```powershell
python -X utf8 artifacts/linear-memory/task-8.3-20261015T235054739Z/audit-acceptance.py
git -c core.excludesFile= diff --check -- .kiro/specs/linear-memory/acceptance.md
```

8.3時点では、タスク8.4で上表の正式runと正式comparisonを再確認した後、**同じ正式run**を入力として既存baseline-saveを明示実行する方針とした。
保存内容とrunの一致、保存後のパス・SHA-256・コマンド・終了値を本記録へ追加し、要件8.6の実証と対応表の状態を更新する計画だった。
8.3ではbaseline-saveを実行しておらず、比較元`wabt-commit-20261016/run-baseline.json`を保持している。
次の比較元は、後述の8.4の保存後に確定した。

## 8.4の明示baseline保存

2026-10-16（JST）、HEAD`29f88c80795128a2b9739a254a772560d245876c`で、比較済みの同じ正式runを次機能の実行結果baselineとして保存した。
[8.3の独立レビュー](../../../artifacts/linear-memory/review-8.3-20261016T000420009Z/review.md)は本仕様の挙動・比較を承認済みであり、保存前に正式run・comparisonと旧比較元の固定SHA-256、全147入力・53,907ケースの結果と比較レコード、以前のpassed16,924件の維持を再確認した。
保存前の確認結果は[before-save.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/before-save.json)にある。

既存CLIのbaseline-saveへ正式runをそのまま入力し、新規の専用ディレクトリへ出力した。
作業ディレクトリは`D:\source\repos\WasmSharp2`、UTC開始は`2026-10-16T00:13:40.2205731Z`、終了は`2026-10-16T00:13:42.2863940Z`、終了コードは0だった。
正確な引数・UTC・終了値は[baseline-save.result.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save.result.json)、出力は[stdout](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save.stdout.log)と[stderr](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save.stderr.log)に保存した。
stdoutは保存完了と保存先を表示し、stderrは空だった。

```powershell
./tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe baseline-save --input artifacts/linear-memory/review-8.2-20261015T234057463Z/run.json --output artifacts/linear-memory/task-8.4-20261016T001148Z/run-baseline.json
```

| 用途 | パス | SHA-256 |
| --- | --- | --- |
| 保存入力の正式run | [review-8.2-20261015T234057463Z/run.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/run.json) | `70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1` |
| 同じrunの正式comparison | [review-8.2-20261015T234057463Z/comparison.json](../../../artifacts/linear-memory/review-8.2-20261015T234057463Z/comparison.json) | `2b1695fd9e23e8178ee825ba2b7dd02c8750f518d1dd842ced2ffb32a0a7f590` |
| 次機能が使う保存baseline | [task-8.4-20261016T001148Z/run-baseline.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/run-baseline.json) | `70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1` |
| 次機能が使う対応manifest | [wabt-commit-20261016/corpus/manifest.json](../../../artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json) | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 保持した旧比較元 | [wabt-commit-20261016/run-baseline.json](../../../artifacts/linear-memory/wabt-commit-20261016/run-baseline.json) | `619961b91dfa83c233864799e7f87323f811e5889c3d6faa7b460812fb72c4f5` |

保存後の[baseline-save-audit.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/baseline-save-audit.json)では、保存baselineと正式runの69,058,822byteすべてが一致した。
全ケースの識別子・順序・詳細、分類件数、埋込みmanifest、実行ポリシー、完了情報、provenanceも一致した。
保存前後で正式run・comparison・manifest・旧比較元・8.3の監査と独立レビュー・tasks.md・保存CLIのexeとDLLのSHA-256は変わらなかった。
旧比較元は上書きしていない。

| 保存metadata・結果 | 確認値 |
| --- | --- |
| 保存元run ID | `75e373e4-c90d-4679-89fe-986b8fc1371e` |
| 保存元run開始UTC | `2026-10-15T23:45:31.3648423+00:00` |
| profile | `core2`、全21機能と147入力を保持 |
| 実行ポリシー | `max_call_depth=1024` |
| 処理・出力の完了 | `processing_complete=true`、`output_complete=true` |
| 入力・ケース | 147入力、53,907ケース |
| passed / failed / runner_error | 23,050 / 0 / 0 |
| runtime_unsupported / blocked / out_of_scope | 1,457 / 28,323 / 1,077 |
| 受入済みcomparisonの回帰・欠落・追加・未比較 | 各0 |

保存前後の監査は[audit-baseline.py](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/audit-baseline.py)で実行し、どちらも終了0だった。
各コマンド・UTC・終了値は[audit-before.result.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/audit-before.result.json)と[audit-after.result.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/audit-after.result.json)、stdout/stderrはそれぞれ同名のログにある。
8.3時点の対応表JSONと未保存記録は保持し、要件8.6の保存証拠だけを補った[最終54項目の対応表](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/coverage-matrix-final.json)を別に保存した。
最終対応表は54項目照合済み、未完了0件である。

次機能は、本節の保存baselineと対応manifestを一組として比較へ使用する。
保存のために公式run/compareや素材再生成を繰り返しておらず、既存の未対応1,457件とblocked28,323件も分類・起因を保持した。
これらは前節で確認したtables-referencesとsimdの対応待ちであり、この保存はCore 2.0全体の完成や全件合格を意味しない。
製品・テストの変更を伴わないため、REDはN/Aとし、実装担当では全テストを追加反復していない。
文書の全54行、出典リンク、保存証拠との一致、変更範囲とdiff --checkは[verify-document.py](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/verify-document.py)で確認し、結果を[verification-summary.json](../../../artifacts/linear-memory/task-8.4-20261016T001148Z/verification-summary.json)に保存した。

## 2026-10-16の実装検証

commit`40107aed6bae67485e5aeb00c8a83a6c30321c5c`に対する[実装検証レポート](../../../artifacts/linear-memory/final-review-20261016T002604Z/report.md)は、linear-memoryの全タスク1〜8と54受入基準をGOと判定した。
同レポートは当該commitに対する履歴として保持し、後続の変更への判定とは区別する。
後続仕様に依存する未対応・前提不成立は残っており、Core 2.0全体の完成を示すものではない。

2026-10-17のレビュー修正後も、8.4で保存したbaselineは更新していない。
修正後の比較で生じた33件（data.wastの15件、global.wastの18件）は、診断の補足文だけの既知の差であり、結果分類・位置・例外型の変化と回帰は0件だった。
次機能の比較では、[レビュー対応の検証記録](tasks.md#claude-codeレビュー対応2026-10-17)と[33件の全件照合](../../../artifacts/linear-memory/claude-review-20261017-4aa3bd52/diagnostic-delta-audit.json)を参照する。
