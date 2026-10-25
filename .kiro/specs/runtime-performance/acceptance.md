# runtime-performanceの検証引継ぎ

2026-10-25時点で、タスク13.3の固定公式スイートは全体run・直前baselineとの比較・同じrunのverifyが成立した。性能の10.3と13.1は未完了・未受入のままであり、仕様全体のGOや13.4のbaseline保存は行っていない。利用者指示に基づくtasks.mdの順序例外により、独立した機能検証を先に進めた記録である。

## 性能の状態

[allocation-fix.md](allocation-fix.md)の計測境界修正後、最終候補のdirect/indirectはそれぞれ229432bytes/opの削減を再現し、事前目標180000bytes/opを満たした。ただし[final-performance-results.md](final-performance-results.md)の全15ケース比較では、時間の反復間変動により5ケースが判定不能となり、production compareは終了1、`IsAccepted=false`である。悪化判定0件を「悪化なし」や全指標の許容内と読み替えない。memory.copy-fillの時間中央値は2960→5098ns（約+72.2%）だった。

[simd-time-investigation.md](simd-time-investigation.md)のSIMD約2倍時間は今回再現せず、原因は未解決として残した。最終SIMD中央値616318→616811ns（約+0.080%）は時間同等だったが、他ケースの判定不能を解消するものではない。追加の通過待ち測定や判断幅の変更は行っていない（6.7、6.8）。

## 同じ最終ソースと実行ファイル

HEADは`544e17db97e1fe7be8c697bbb89fb0f375a1fa0a`。未コミット差分を含むため、HEADだけで同定しない。[source-before.json](../../../artifacts/runtime-performance/task-13.3-20261025/source-before.json)と[source-after.json](../../../artifacts/runtime-performance/task-13.3-20261025/source-after.json)で、13.1と同じ候補runtime集合`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`、共通harness集合`c3f92f2403345611b998a7420dc9054be38c6e190c3618115d3c60170f09fa8f`を確認した。SDK10.0.402、.NET10.0.13、Windows x64を使用した。

| 使用ファイル（tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/配下） | SHA-256 |
| --- | --- |
| WasmSharp.dll | `c8b71f5b3b627bf9976bf737a754a34478d772855194a8cac08e922e5e7ea32e` |
| WasmSharp.TestSuiteRunner.dll | `4abc6b94b6cb83204b4aa841aa04ab6cfcb37f07fe0ffb958ac0f87e61f4c91c` |
| WasmSharp.TestSuiteRunner.exe | `b27a9363a7d0f8677a3a095fdd4c15498d16bf3be8ed0d134ffaa6c8f9163f8e` |

今回のruntimeは性能最終B1/B2/B3のcollectorが記録したruntimeとbyte単位で一致する。BDN子processのruntimeは別ビルドの`e79a314a9b5428fec16cdad6d8b3c5563748e9786595e8dd09e5d8b83f11206c`であり、公式runnerと同じbinaryとは扱わない。同じruntime source集合であることと、collector artifactの同一性を[before.json](../../../artifacts/runtime-performance/task-13.3-20261025/before.json)に対応付けた。

同sourceに対する[既存canonical Releaseビルド](../../../artifacts/runtime-performance/task-10.3-resume-20261025/independent-review/build.log)の警告・エラー0を参照した。公開契約の検証は[regression-results.md](regression-results.md)にある13.2の5981件とHW無効SIMD81件、harness修正後は既存の独立Benchmarks332件成功を参照する。今回、製品・テストを変更しておらず、重複ビルド・全単体テストは行っていない。

## 固定素材と比較元

| 対象 | 記録・SHA-256 |
| --- | --- |
| 固定manifest | `artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json`、`c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 直前受入baseline | `artifacts/simd/task-10.3-20261018T215944Z/run-baseline.json`、`31b0282af0cc5b7b528a660ae409349c2a748538ac8e58fd3a0da2e31e387d6d` |
| baseline run ID | `1820ada6-3530-46ce-895f-0593e10600b7` |
| core2-profile.json | `53e65ededab6814a6f73ac17ff286cedb35221b01aea83bbdd20e0ab15b452bc` |
| 固定spec / WABT commit | `05ca4182176763112561ae20153975c12bd689e4` / `c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db` |

直前の[SIMD受入10.2/10.3](../simd/acceptance.md#102-単一最終runによる公式比較と全体判定)と[親の確認](../../../artifacts/simd/task-10.3-20261018T215944Z/parent-verification.md)を確認した。147公式sourceと5821素材のhashはmanifestと一致し、SIMD ON・relaxed-SIMD OFF、feature状態7ON/14OFFを維持した。素材の再生成、期待値・診断照合の緩和、対象集合の縮小、baselineの変更はない。

## 今回の公式機能検証

記録先は[task-13.3-20261025](../../../artifacts/runtime-performance/task-13.3-20261025/)。既存CLIを使い、各操作を個別に実行した。完全なargv・cwd・開始終了UTC・終了値・原ログhash・入力hashは各`*-command.json`に保存した。

| 操作 | 2026-10-25のUTC | 終了値・結果 |
| --- | --- | --- |
| 全体run | 01:35:09.376〜01:35:14.223 | 0、147入力・53907commandを記録 |
| 同じrunのcompare-run | 01:35:18.945〜01:35:23.816 | 0、比較成立・完了 |
| 同じrunのverify | 01:35:29.234〜01:35:30.726 | 0、「最終判定: 合格」 |

実行コマンドは以下のとおり。既存出力の再利用はできないため、再実行時は新しい出力先を指定する。

```powershell
$runner = 'tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe'
& $runner run --manifest artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json --output artifacts/runtime-performance/task-13.3-20261025/run.json
& $runner compare-run --baseline artifacts/simd/task-10.3-20261018T215944Z/run-baseline.json --current artifacts/runtime-performance/task-13.3-20261025/run.json --output artifacts/runtime-performance/task-13.3-20261025/comparison.json
& $runner verify --input artifacts/runtime-performance/task-13.3-20261025/run.json
```

採用する単一runのIDは`c885f06d-4f62-4776-9976-87c6d8b76551`、[run.json](../../../artifacts/runtime-performance/task-13.3-20261025/run.json)のSHA-256は`303dff0205b5ad9f68ee0287c302f69663a3187fcd856744325a397336091ab0`。[comparison.json](../../../artifacts/runtime-performance/task-13.3-20261025/comparison.json)は`376d4c5755d07074b1c9e46c838c0b06b99a73772bdd9c8e6c8f1a8a481750e9`。compare/verify前後で同じrunのhashを確認した。

- passed52830件、out_of_scope1077件。failed・runtime_unsupported・command/inputのrunner_error・blockedは0件。
- 入力異常・未処理・件数未確定、以前passed52830件の回帰・欠落、全ケースの欠落・追加・未比較は0件。
- 比較の条件差・出典差・入力素材差・ケース変化はすべて0件。全53907件のID・行・command種別・category・期待診断・期待値はbaselineと一致する。
- out_of_scope1077件の集合はbaselineおよび公式JSONの`module_type=text`の集合と一致し、合格件数に含めない。
- manifest・profile・execution_policyと入力順序は不変。source・実行artifact・公式source/素材・参照記録の計6249ファイルの前後hash差は0件。

合否判定は既存CLIによる。補助scriptはファイル同一性・保存結果の件数と集合を照合するもので、Wasmの期待値判定を実装しない。詳細は[results-audit-final.json](../../../artifacts/runtime-performance/task-13.3-20261025/results-audit-final.json)を参照する。今回の主張は固定profile・固定入力に対する13.3の機能条件の成立に限定する（8.2、8.3）。

## 未実施範囲と引継ぎ

[独立レビュー](../../../artifacts/runtime-performance/review-13.3-20261025/review-verdict.md)は、同じrunへのcompare-run・verifyの再実行がともに終了0、固定6249ファイルと全53907commandを含む357項目の照合成功で、13.3の機能範囲のみAPPROVEDとした。[親の完了確認](../../../artifacts/runtime-performance/task-13.3-20261025/parent-completion.json)も現行runtime/harness152ファイル・原ログ・run・旧baselineのhash一致によりVERIFIEDとし、13.3を完了に更新した。

13.4のbaseline-save、性能10.3/13.1の受入、仕様全体のGO、Ubuntu・他CPU環境、任意入力・対象外proposalの検証は行っていない。今回の機能成功で性能の判定不能や未解決の時間変動を解消済みとしない。
