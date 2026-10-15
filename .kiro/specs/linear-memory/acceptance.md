# linear-memoryの受入準備記録

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
