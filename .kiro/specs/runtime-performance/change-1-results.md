# 第1変更の比較結果

**第1変更の採用は保留する。タスク10.3は未完了。** 予定した交互6run・90ケースの測定と保存は完了したが、固定policyによる比較で完了を阻害する理由が残った。割り当ての削減観測を、性能改善の受入や次の変更の比較元へ進める根拠にしない。

production `ComparisonReport.IsAccepted=false`、compare終了値`1`、両指標の許容内を確認できた数`23/30`、阻害ケース`7`件、条件差`0`件。詳細は[comparison.json](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/comparison/comparison.json)と[verification.json](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/verification.json)を参照する。

## 固定した版と条件

| 項目 | 基準版B | 候補版C |
| --- | --- | --- |
| revision | `6fdd7e28d629238306cd5e97960214cafdff8da4`と保存差分 | `6c96445425a4e29b62f6f991db4cacddc1535764` |
| runtime集合SHA-256 | `4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751` | `0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87` |
| harness集合SHA-256 | `bfdd7e94b71ecbf21c50669310920af24dd10616861790f5d668b926609c824d` | 同左 |
| 使用ディレクトリ | `C:/Users/taihe/AppData/Local/Temp/wasmsharp-baseline-9.1-10c52c20ac284cf89b401fce851df9a6` | `D:/source/repos/WasmSharp2` |

runtimeの差は`Interpreter.cs`と`InterpreterContext.cs`に限定され、タスク10.1の責務整理と10.2の正常入口での不要な`WasmFailureLocation`生成の除去に対応する。harnessは変更していない。基準復元先には以前からREADMEだけの保存差分があり、source集合の一致を確認した。測定中のsource/assembly変更は全runで0件だった。

- Windows`Microsoft Windows 10.0.26300`、x64、Intel Core i7-13700K、論理24processor。
- SDK`10.0.402`、runtime`.NET 10.0.13; 10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`、BDN`0.15.8+1e8238ef6c08e18821edae83ac8d6b0a2bb1bb98`。
- Release、Measurement、Throughput、1launch、Workstation GC、RyuJIT、高パフォーマンスplan`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`、AC接続。電源planは変更していない。
- Jobのpilot・warmup・本測定の反復調整はBDN既定を維持し、別build/test/profileを並行実行していない。各launchは新規process。Invokeケースは準備済みmodule/instanceを使用し、準備処理ケースの区間は固定したケース定義に従う。
- GC/JITの内部実効値で公開APIから取得できない項目は、保存された`Unavailable`を保持し、既定値から推測補完していない。

素材は`task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json`、SHA-256`b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a`、casesのSHA-256`bf0a93272f03e4bbb0717634724f39cc6d0f2169bc9fb84825614cf566ad0389`。6素材・15ケースの入力・期待値・仕事量・状態復元を維持した。

事前policyは[comparison-policy.json](comparison-policy.json)、ID`runtime-performance-original-and-change-1-20261024`、SHA-256`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`。目標、許容増加、同等範囲、ノイズ、反復間判断幅を測定結果に合わせて変更していない。計画と導出は[optimization-plan.md](optimization-plan.md)を参照する。

## 実行順序と保存run

測定前に[Task Brief](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/task-brief.md)と[固定条件・事前順序](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/preflight.json)を保存し、B1→C1→B2→C2→B3→C3へ固定した。[旧基準run](baseline.md)は導出の記録として保持し、今回の比較入力へ流用していない。両版のReleaseビルドは警告・エラー0、両版の`check`は全15ケース成功・終了0。

| 順序 | run ID | 開始〜完了UTC | run.jsonのSHA-256 | 保存先・コマンド・終了値 |
| --- | --- | --- | --- | --- |
| 1・baseline-1 | `c7359561fe9c48dfa321cc837bcea21f` | `2026-10-24T12:58:51.8561818+00:00`〜`2026-10-24T13:05:02.6261716+00:00` | `c45dc8f0813ea92550efbb470e8d87c5d98f62992aac0ffe34e02e03f5d63633` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-1/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-1.command.json)・`0` |
| 2・candidate-1 | `d5bcf5955452489d99bd53cf57744c9a` | `2026-10-24T13:05:03.8002292+00:00`〜`2026-10-24T13:10:36.8781731+00:00` | `3a87da7ae051bb7f25822123d6004b24dfa70c18e4af826beddf4d1a97888834` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-1/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-1.command.json)・`0` |
| 3・baseline-2 | `42ed3c885fd543eabbdd7e05c101d35d` | `2026-10-24T13:10:37.7052838+00:00`〜`2026-10-24T13:16:39.0213076+00:00` | `d33bba65141fed37738f70385936c7ed905493fabcb21a1331e8e6173c5e1f9e` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-2/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-2.command.json)・`0` |
| 4・candidate-2 | `b92bc92645f24709ab3f04a44abfc7f6` | `2026-10-24T13:16:39.934656+00:00`〜`2026-10-24T13:22:07.1625452+00:00` | `e41a51fdd15a2d0060f3e956abef7085039d39b0a917e2550d956faa82557a83` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-2/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-2.command.json)・`0` |
| 5・baseline-3 | `7a5cd2ed8de543faa7b36bc2faaccb70` | `2026-10-24T13:22:07.9735952+00:00`〜`2026-10-24T13:28:15.4440754+00:00` | `5cf935eb77b1e219793a9edcdf749db9767019ea83721053070eecc80b79fc1e` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-3/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/baseline-3.command.json)・`0` |
| 6・candidate-3 | `1e7c9c86ef7342f3912dfac7d6ad0065` | `2026-10-24T13:28:16.2640311+00:00`〜`2026-10-24T13:33:34.5278475+00:00` | `ab3769e5c63d3b7612d973aa16dddd93e811ffaa458ee444798196c3ff2071d3` | [run](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-3/run.json)・[command](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/candidate-3.command.json)・`0` |

全6runは独立したIDを持つ。全90ケースで`Succeeded`、子終了0、Errors空、同じcase/PIDの前後検査成功、必須Time・丸め前Allocation・GC・rawの保存を確認した。production `RunStore.Load`で保存参照とhashを読み直し、`RunConditions.Create`とcompareで条件差を確認した。失敗・欠落を0へ補完していない。

群別の保存契約照合・個別時間統計・99.9%CI・割り当て総bytes/操作数・GC全世代総回数・raw Actual/Result反復数は[基準群summary](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/summary-baseline/summary.json)、[候補群summary](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/summary-candidate/summary.json)、[基準群CSV](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/summary-baseline/measurements.csv)、[候補群CSV](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/summary-candidate/measurements.csv)に保存した。各run内の`raw/`と`checks/`が元の観測を保持する。

## 全15ケースの個別結果

時間の代表値は独立runの平均ns/opの中央値、割り当ての代表値は丸め前bytes/opの中央値。表の時間は小数第3位まで表示し、完全な値はJSON/CSVに保存する。個値の順序は各版1→2→3。幅は最小〜最大、差はC−B、比率はC/B。時間のCIは各run内のバッチ反復の統計であり、独立群の差のCIや個々のInvokeの遅延分布ではない。

### 時間（ns/op）

| ケース | B個値（1,2,3） | C個値（1,2,3） | B中央値［幅］ | C中央値［幅］ | 差C−B | 比率C/B | 分類・理由 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `decode-validate.large` | 158,203.743, 151,394.012, 154,573.331 | 145,545.120, 157,040.685, 150,684.323 | 154,573.331［151,394.012〜158,203.743］ | 150,684.323［145,545.120〜157,040.685］ | -3,889.009 | 0.974840365（-2.516%） | 同等・同等範囲内 |
| `decode-validate.small` | 2,338.791, 2,294.648, 2,327.985 | 2,226.205, 2,261.733, 2,305.872 | 2,327.985［2,294.648〜2,338.791］ | 2,261.733［2,226.205〜2,305.872］ | -66.251 | 0.971541379（-2.846%） | 同等・同等範囲内 |
| `decode.large` | 56,249.206, 50,721.473, 52,425.069 | 50,156.458, 51,386.388, 51,641.321 | 52,425.069［50,721.473〜56,249.206］ | 51,386.388［50,156.458〜51,641.321］ | -1,038.681 | 0.980187321（-1.981%） | 判定不能・反復間ばらつき超過 |
| `decode.small` | 825.069, 794.926, 799.060 | 783.442, 786.346, 758.258 | 799.060［794.926〜825.069］ | 783.442［758.258〜786.346］ | -15.619 | 0.980453688（-1.955%） | 同等・同等範囲内 |
| `instantiate.large` | 6,067.883, 6,028.609, 6,407.544 | 6,302.832, 6,340.984, 6,051.192 | 6,067.883［6,028.609〜6,407.544］ | 6,302.832［6,051.192〜6,340.984］ | 234.948 | 1.038720008（+3.872%） | 判定不能・反復間ばらつき超過 |
| `invoke.empty` | 51.527, 49.740, 51.427 | 46.310, 45.141, 45.904 | 51.427［49.740〜51.527］ | 45.904［45.141〜46.310］ | -5.523 | 0.892608912（-10.739%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `invoke.multi` | 318.586, 318.973, 318.428 | 317.829, 311.839, 315.233 | 318.586［318.428〜318.973］ | 315.233［311.839〜317.829］ | -3.353 | 0.989475542（-1.052%） | 同等・同等範囲内 |
| `invoke.single` | 275.082, 271.406, 276.879 | 277.852, 269.204, 271.611 | 275.082［271.406〜276.879］ | 271.611［269.204〜277.852］ | -3.471 | 0.987382056（-1.262%） | 判定不能・反復間ばらつき超過 |
| `kernel.matrix` | 4,445,888.895, 4,836,230.599, 4,848,138.542 | 4,450,209.796, 4,465,702.031, 4,607,643.854 | 4,836,230.599［4,445,888.895〜4,848,138.542］ | 4,465,702.031［4,450,209.796〜4,607,643.854］ | -370,528.568 | 0.923384843（-7.662%） | 判定不能・反復間ばらつき超過 |
| `loop.float-host` | 1,270,364.935, 1,288,217.331, 1,248,291.289 | 1,260,214.202, 1,306,917.084, 1,294,843.893 | 1,270,364.935［1,248,291.289〜1,288,217.331］ | 1,294,843.893［1,260,214.202〜1,306,917.084］ | 24,478.958 | 1.019269233（+1.927%） | 同等・同等範囲内 |
| `loop.indirect` | 1,039,850.285, 1,060,499.837, 1,045,363.932 | 996,511.091, 995,359.915, 1,040,149.089 | 1,045,363.932［1,039,850.285〜1,060,499.837］ | 996,511.091［995,359.915〜1,040,149.089］ | -48,852.841 | 0.953267145（-4.673%） | 判定不能・反復間ばらつき超過 |
| `loop.integer-direct` | 1,110,410.117, 1,094,092.708, 1,108,612.201 | 1,065,591.671, 1,042,311.537, 1,084,770.977 | 1,108,612.201［1,094,092.708〜1,110,410.117］ | 1,065,591.671［1,042,311.537〜1,084,770.977］ | -43,020.529 | 0.961194249（-3.881%） | 判定不能・同等範囲外・許容内 |
| `memory.copy-fill` | 2,192.522, 2,543.543, 2,498.154 | 2,208.821, 2,232.112, 2,234.611 | 2,498.154［2,192.522〜2,543.543］ | 2,232.112［2,208.821〜2,234.611］ | -266.042 | 0.893504392（-10.650%） | 判定不能・反復間ばらつき超過 |
| `scan.scalar` | 1,631,756.599, 1,589,590.872, 1,681,620.968 | 1,684,377.734, 1,684,174.358, 1,635,763.542 | 1,631,756.599［1,589,590.872〜1,681,620.968］ | 1,684,174.358［1,635,763.542〜1,684,377.734］ | 52,417.759 | 1.032123516（+3.212%） | 同等・同等範囲内 |
| `scan.simd` | 421,811.332, 422,381.667, 426,846.589 | 435,694.245, 402,365.830, 433,177.865 | 422,381.667［421,811.332〜426,846.589］ | 433,177.865［402,365.830〜435,694.245］ | 10,796.198 | 1.025560290（+2.556%） | 判定不能・反復間ばらつき超過 |

### managed割り当て量（bytes/op）

| ケース | B個値（1,2,3） | C個値（1,2,3） | B中央値［幅］ | C中央値［幅］ | 差C−B | 比率C/B | 分類・理由 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `decode-validate.large` | 723600, 723600, 723600 | 723600, 723600, 723600 | 723600［723600〜723600］ | 723600［723600〜723600］ | 0 | 1.000000000（+0.000%） | 同等・同等範囲内 |
| `decode-validate.small` | 9112, 9112, 9112.000122070312 | 9112, 9112, 9112 | 9112［9112〜9112.000122070312］ | 9112［9112〜9112］ | 0 | 1.000000000（+0.000%） | 同等・同等範囲内 |
| `decode.large` | 360496, 360496, 360496 | 360496, 360496, 360496.001953125 | 360496［360496〜360496］ | 360496［360496〜360496.001953125］ | 0 | 1.000000000（+0.000%） | 同等・同等範囲内 |
| `decode.small` | 4328, 4328, 4328 | 4328, 4328, 4328 | 4328［4328〜4328］ | 4328［4328〜4328］ | 0 | 1.000000000（+0.000%） | 同等・同等範囲内 |
| `instantiate.large` | 78720, 78720, 78720.000244140625 | 78664, 78664, 78664 | 78720［78720〜78720.000244140625］ | 78664［78664〜78664］ | -56 | 0.999288618（-0.071%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `invoke.empty` | 184, 184, 184 | 128, 128, 128 | 184［184〜184］ | 128［128〜128］ | -56 | 0.695652174（-30.435%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `invoke.multi` | 640.000015258789, 640, 640 | 584, 584, 584 | 640［640〜640.000015258789］ | 584［584〜584］ | -56 | 0.912500000（-8.750%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `invoke.single` | 384, 384, 384 | 328, 328, 328 | 384［384〜384］ | 328［328〜328］ | -56 | 0.854166667（-14.583%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `kernel.matrix` | 656, 656, 656 | 600, 600, 600 | 656［656〜656］ | 600［600〜600］ | -56 | 0.914634146（-8.537%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `loop.float-host` | 557504, 557504, 557504 | 557448, 557448, 557448 | 557504［557504〜557504］ | 557448［557448〜557448］ | -56 | 0.999899552（-0.010%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `loop.indirect` | 229928, 229928, 229928 | 496, 496, 496 | 229928［229928〜229928］ | 496［496〜496］ | -229432 | 0.002157197（-99.784%） | 改善・削減目標達成 |
| `loop.integer-direct` | 230176, 230176, 230176 | 744, 744, 744 | 230176［230176〜230176］ | 744［744〜744］ | -229432 | 0.003232309（-99.677%） | 改善・削減目標達成 |
| `memory.copy-fill` | 416, 416, 416 | 360, 360, 360 | 416［416〜416］ | 360［360〜360］ | -56 | 0.865384615（-13.462%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `scan.scalar` | 448, 448, 448 | 392, 392, 392 | 448［448〜448］ | 392［392〜392］ | -56 | 0.875000000（-12.500%） | 改善・同等範囲・ノイズ幅を超える削減 |
| `scan.simd` | 448, 448, 448 | 392, 392, 392 | 448［448〜448］ | 392［392〜392］ | -56 | 0.875000000（-12.500%） | 改善・同等範囲・ノイズ幅を超える削減 |

managed割り当てはprocess全体のmanaged割り当て観測で、時間測定とは別の追加反復の総bytes/操作数を用いる。保持量・ピーク量・nativeメモリ・guest線形メモリ内の使用量ではない。BDNの整数表示を比較に使わず、表示0を割り当て不存在の証明にしない。

### 目標と阻害理由

| 対象 | 削減量bytes/op | 180000bytes/op目標 | 時間の許容内 | 割り当ての許容内 | 完了阻害 |
| --- | --- | --- | --- | --- | --- |
| `loop.indirect` | 229432 | 確認済み | 判定不能 | 確認済み | あり |
| `loop.integer-direct` | 229432 | 確認済み | 確認済み | 確認済み | なし |

時間短縮は割り当て削減の追加条件にしない。ただし、時間が同等・許容内であることを判断できなければ、割り当ての目標達成だけで変更全体を受け入れない。

以下が正式比較で残った阻害理由。run値や観測を選別せず、最初の6runをそのまま保存した。原因はこの結果だけから断定せず、後続の診断へ引き継ぐ。

- `decode.large`: `excessive_variation` 時間: run群 [c7359561fe9c48dfa321cc837bcea21f, 42ed3c885fd543eabbdd7e05c101d35d, 7a5cd2ed8de543faa7b36bc2faaccb70]が中央値からの反復間判断幅2883.378782145182を超えています。 `within_equivalence` 割り当て: 事前の同等範囲内です。
- `instantiate.large`: `excessive_variation` 時間: run群 [c7359561fe9c48dfa321cc837bcea21f, 42ed3c885fd543eabbdd7e05c101d35d, 7a5cd2ed8de543faa7b36bc2faaccb70]が中央値からの反復間判断幅242.715329683744を超えています。 `improvement_beyond_noise` 割り当て: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。
- `invoke.single`: `excessive_variation` 時間: run群 [c7359561fe9c48dfa321cc837bcea21f, 42ed3c885fd543eabbdd7e05c101d35d, 7a5cd2ed8de543faa7b36bc2faaccb70]が中央値からの反復間判断幅2.750818472642165を超えています。 `excessive_variation` 時間: run群 [d5bcf5955452489d99bd53cf57744c9a, b92bc92645f24709ab3f04a44abfc7f6, 1e7c9c86ef7342f3912dfac7d6ad0065]が中央値からの反復間判断幅2.716108798980713を超えています。 `improvement_beyond_noise` 割り当て: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。
- `kernel.matrix`: `excessive_variation` 時間: run群 [c7359561fe9c48dfa321cc837bcea21f, 42ed3c885fd543eabbdd7e05c101d35d, 7a5cd2ed8de543faa7b36bc2faaccb70]が中央値からの反復間判断幅241811.52994791666を超えています。 `improvement_beyond_noise` 割り当て: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。
- `loop.indirect`: `excessive_variation` 時間: run群 [d5bcf5955452489d99bd53cf57744c9a, b92bc92645f24709ab3f04a44abfc7f6, 1e7c9c86ef7342f3912dfac7d6ad0065]が中央値からの反復間判断幅19930.22181919643を超えています。 `reduction_target_reached` 割り当て: 事前の削減目標を満たし、基準ノイズ幅を超えています。
- `memory.copy-fill`: `excessive_variation` 時間: run群 [c7359561fe9c48dfa321cc837bcea21f, 42ed3c885fd543eabbdd7e05c101d35d, 7a5cd2ed8de543faa7b36bc2faaccb70]が中央値からの反復間判断幅74.94462822579048を超えています。 `improvement_beyond_noise` 割り当て: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。
- `scan.simd`: `excessive_variation` 時間: run群 [d5bcf5955452489d99bd53cf57744c9a, b92bc92645f24709ab3f04a44abfc7f6, 1e7c9c86ef7342f3912dfac7d6ad0065]が中央値からの反復間判断幅23824.782552083332を超えています。 `improvement_beyond_noise` 割り当て: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。

各時間群の中央値からの最大偏差は次のとおり。比較不能の理由を具体化する観測値であり、固定した幅を更新するための設定値ではない。

| ケース | B最大偏差 | C最大偏差 | 固定RunVariation |
| --- | --- | --- | --- |
| `decode.large` | 7.29448% | 2.39349% | 5.50000% |
| `instantiate.large` | 5.59768% | 3.99249% | 4.00000% |
| `invoke.single` | 1.33628% | 2.29779% | 1.00000% |
| `kernel.matrix` | 8.07120% | 3.17849% | 5.00000% |
| `loop.indirect` | 1.44791% | 4.37908% | 2.00000% |
| `memory.copy-fill` | 12.23431% | 1.04345% | 3.00000% |
| `scan.simd` | 1.05708% | 7.11302% | 5.50000% |

群内の中央値からの最大偏差と固定policyを[comparison-supplement.json](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/comparison-supplement.json)へ、全30指標の完全な値を[metric-comparison.csv](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/metric-comparison.csv)へ保存した。再開には、阻害した測定条件・原因の診断と、元の目標・許容幅・同等範囲を保持した再評価が必要。受入harnessの修正が必要なら、進行中の候補受入を中止し、旧runtimeと修正harnessの基準値を取り直す。更新できるのは基準側の再測定に根拠があるノイズ・反復間判断幅だけであり、候補に合わせて後付けしない。

## 再実行と再集計

実行時の完全なコマンド・cwd・UTC時刻・終了値は各runのcommand/exitファイル、build/checkの同名記録に保存した。交互測定手順は[Measure-Alternating.ps1](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/Measure-Alternating.ps1)。元の出力先を再利用せず、新しい記録ディレクトリを用意する。測定コマンドは両版のcwdから同じ引数で実行した。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- check --assets <採用manifestの絶対path>
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets <同manifestの絶対path> --case all --kind measurement --output <未作成の各run出力先>
```

今回の保存契約照合とproduction比較は[Verify-And-Compare.ps1](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/Verify-And-Compare.ps1)、群別のraw再集計は[Summarize-Group.ps1](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/Summarize-Group.ps1)を使った。再集計では、`-SourceRole baseline`または`candidate`、対応する3runの`-RunDirectories`、新規の`-OutputDirectory`、ビルド済み測定assemblyと採用manifestを渡す。比較は次のコマンドへB1〜B3とC1〜C3をそれぞれカンマ区切りの1引数で渡す。

```powershell
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- compare --baseline <B1,B2,B3の絶対pathを1引数> --current <C1,C2,C3の絶対pathを1引数> --policy .kiro/specs/runtime-performance/comparison-policy.json --output <未作成の比較出力先>
```

集計・比較前後で保存入力`672`ファイルのhash不一致0件。policyのhashも前後で一致する。固定source確認と最終hash一覧は[final-check.json](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/final-check.json)と[final-file-hashes.json](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/final-file-hashes.json)へ保存する。

最初の補助verification生成は、PowerShellの集合プロパティ参照で失敗した。測定・両群の保存契約照合・正式compareの完了後に発生したもので、測定harnessは変更していない。元の[失敗ログ](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/verify-and-compare.log)を保持し、別の[Verify-Comparison.ps1](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/Verify-Comparison.ps1)で保存済み比較をproduction型へ読み直した。再確認は終了0、保存入力hash不一致0件で、正式compareの終了1・未採用判定を保持している。

最終照合では、測定に使った両版のHEAD、元リポジトリのindexとlocal refs、固定基準ディレクトリのstatusは不変だった。一方で`refs/remotes/origin/HEAD`と`refs/remotes/origin/main`は、事前の`6c96445425a4e29b62f6f991db4cacddc1535764`から`2fa68483fc13363867c041a517e886f7e809fd54`へ変わっていた。本タスクはGitを変更するコマンドを実行していない。最初の[厳密なrefs照合の停止ログ](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/final-check.log)も保持し、[別の最終確認手順](../../../artifacts/runtime-performance/task-10.3-20261024T125550Z/Finalize-Recorded.ps1)でremote refsの差を明記した。測定に使ったsource/harness・policy・素材・保存入力のhash不変と、比較条件差0件の観測を保持する。

## 保持観測・回帰確認・未実施範囲

今回の変更は不要な診断位置の生成を失敗分岐へ移すだけで、buffer/contextの再利用、保持容量・期間の拡大、instance/externrefのroot追加は行っていない。[事前計画](optimization-plan.md#責務と所有を先に整理する)どおり要件4.5のRetention系列、root観測、WeakReferenceテストは非該当で、実行していない。

意味論と公開契約の確認は、[タスク10.2の検証記録](../../../artifacts/runtime-performance/task-10.2-20261024T123953Z/final-check.json)と[独立レビュー](../../../artifacts/runtime-performance/review-10.2-20261024T125100Z/verdict.md)のRelease警告・エラー0、全5969テスト成功（4799+160+702+308）を引き継ぐ。返却済み結果・callback引数・locals・importの同一性・定義instance、同期再入と外側policy、正常/trap/exhaustion/同じホスト例外実体の伝播後の復元は同タスクの検証範囲である。本タスクは計測・保存・文書化のみなので新しいテストやフラグを追加していない。今回も両版のReleaseビルド後に全15ケースのcheckと測定中の前後検査を行った。

最終版と元基準の全15ケース再測定（13.1）、固定公式スイート全体のrun/比較/verify（13.3）、Ubuntuでの実行は未実施。この記録は仕様全体の受入や任意入力・別環境の性能保証ではない。

## 10.3再試行1: affinity記録の修正と基準側の再特徴化

初回の未採用記録を保持し、受入用harness修正の設計経路を実施した。`BenchmarkConfig.Create`はWindows/Linuxで親processの現在のaffinityをJobへ明示する。設定と保存値を確かめるテストは修正前に失敗し、修正後はRelease警告・エラー0、測定契約310件成功。保存したJobのmaskが異なれば、CPU数が同じでも比較条件差として返ることを確認した。ランタイム・生成器・素材・計時区間は変更していない。

旧runtimeは9.2のrun-1から元repo外の新規tempへ復元し、修正harnessだけを組み合わせた。旧runtime集合`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`、候補runtime集合`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`を保持し、新harness集合は両側とも`8b026cd35c8b10190d44355ffb075987e1ba2bdd151b20d0e314989eb66a3211`。既存の固定復元先は変更していない。

私有workerだけをCPU14/mask16384と`DOTNET_TieredPGO=0`へ設定し、旧runtimeの全15ケースを独立3run直列測定した。全45caseがSucceeded、全子process終了0・Errors空・build/validation成功・前後検査成功、必須指標・GC・raw・保存hashが成立した。既存のRunConditionsによる条件差0、全45BDN子と3collectorの実mask16384、起動時CPU数1、HighPerformance/ACを確認し、workerの元mask16777215と未設定のPGO環境変数を復元した。候補測定は開始していない。

一方、新条件の基準側にも以下の不安定性が残った。下表は3runすべてを記載し、runの選別はしていない。

| 指標 | case ID | 基準1 | 基準2 | 基準3 | 元の反復間判断幅 |
| --- | --- | ---: | ---: | ---: | ---: |
| 時間ns/op | `scan.simd` | 598652.258 | 1200096.229 | 613192.112 | 5.5% |
| 時間ns/op | `memory.copy-fill` | 2524.445 | 2165.508 | 2507.899 | 3% |
| 割当bytes/op | `kernel.matrix` | 656 | 708.5 | 708.5 | 1bytes/op |
| 割当bytes/op | `loop.indirect` | 229934.5625 | 229928 | 229934.5625 | 1bytes/op |

`scan.simd`の中央値からの最大偏差は95.712%、`memory.copy-fill`は13.652%。matrixの割当差52.5bytes/opは同じ32操作で総量差1680bytes、indirectの差6.5625bytes/opも同じ256操作で総量差1680bytesに対応する。これらは観測であり、CPU、PGO、警告などのどれが原因かをこの測定だけで確定していない。

BDNは45子すべての起動直後に`Failed to set up processor affinity 0x4000`と「パラメーターが間違っています。」を出力した。警告を含む元のログを保存し、全実PIDが要求mask16384を継承・維持したことと、validation成功・Errors空・終了0を別途照合した。正確な文言と前後の行は[measurement-conditions.json](../../../artifacts/runtime-performance/task-10.3-retry1-20261024T141717Z/measurement-conditions.json)に保存した。警告が時間モードや1680bytes差の原因であるとは断定しない。

大きい時間モードを広い幅で覆わない条件に従い、Noise/RunVariationの新版policyは作成していない。元`comparison-policy.json`のSHA-256は`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`のまま、全15caseの削減目標、AllowedIncrease、Equivalence、必要独立run数を保持している。基準の比較前提が未成立なので10.3はBLOCKEDであり、改善受入・完了とはしない。再開にはこの不安定性の原因調査と、元目標・許容幅で判断できる基準の確立が必要となる。

記録は[task-10.3-retry1-20261024T141717Z](../../../artifacts/runtime-performance/task-10.3-retry1-20261024T141717Z/)。全15caseの値は`observed-values.md`、丸め前値・CI・操作数・条件とrunのhashは`summary-baseline/`、阻害要因は`instability-summary.json`、元目標の保持は`goal-preservation.json`を参照。再利用は非選定のままで、Retention、候補交互6run、新版policyの独立レビュー、最終公式全体検証は未実施。

## 10.3再試行2: 子processのaffinity観測と短期ゲートの停止

[debug round2の結果](../../../artifacts/runtime-performance/debug-10.3-r2-20261024T144426Z/debug-report.md)に従い、Jobへの設定要求と子processの実際の前後maskを分離した。再試行1のWithAffinityは、BDN0.15.8がProcessorCount=1でmask16384を0へ切り詰めるため省いた。Jobの要求値は既定のまま保存し、親のmaskを保証した扱いにしない。既存BenchmarkProcessのSetup/Cleanupが計時外でProcess.ProcessorAffinityを採取し、ProcessCheckへ前後値と取得不能理由を保存する。Windows/Linuxの同APIが返すmaskを対象とし、未対応OSや取得不能はnullと理由で記録する。0/Defaultで観測値を補わない。

RunStoreの既存保存・対応検証を再利用し、全予定case/Job/launchの観測欠落、取得不能、前後差、同じJobの子process間差、run間差をRunConditions/RunComparisonで受け入れない。旧JSONは読込・往復保存を維持するが、実観測のない旧runは新しい比較で条件不足となる。設定と保存の契約変更なのでFeature Flag Protocolは非該当。ランタイム・生成器・素材・15case・操作数・計時区間・LaunchCount・pilot/warmupは変更していない。

契約REDは60件中8件失敗で、前後maskの未保存と欠落/不一致の見逃しを確認した。初回全件では旧JSON往復2件が追加null項目で失敗したため、追加nullable項目をnull時省略として互換性を維持した。最終Releaseビルドは警告・エラー0、Benchmarks.Tests320件成功・失敗/スキップ0。旧runtimeのcheckは15件成功、別processのSmokeも終了0・Errors空・前後検査成功・mask16384/16384・affinity警告0だった。Smokeの数値は基準値へ採用しない。

新規復元先は`C:/Users/taihe/AppData/Local/Temp/wasmsharp-baseline-10.3-retry2-b8090524f14b4e2db389b7a70b77d2c3`。旧runtime集合`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`、候補runtime集合`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`は維持し、両側新harness集合は`5b66c040c551df97c7964984140dc75f88ad91f2e8da7dfb02c3df36dc4af6f8`で一致した。既存2つの復元先は変更していない。

短期ゲートは実行前にscan.simd×3→memory.copy-fill×3→kernel.matrix×3→loop.indirect×3、case個別の計12独立Measurement runへ固定した。CPU14/mask16384、DOTNET_TieredPGO=0、HighPerformance/AC、SDK10.0.402/runtime10.0.13、BDN0.15.8を維持し、診断環境変数やtraceを混ぜない。workerは子processのWaitForExitだけを行い、observerはCPU14を除外した。

| case | run | 平均ns/op | 総割当bytes | 操作数 | 丸め前bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| scan.simd | 1 | 613124.1286057692 | 231056 | 512 | 451.28125 |
| scan.simd | 2 | 613402.4693080357 | 231056 | 512 | 451.28125 |
| scan.simd | 3 | 623484.5005580357 | 231056 | 512 | 451.28125 |
| memory.copy-fill | 1 | 2559.089543269231 | 54525952 | 131072 | 416 |
| memory.copy-fill | 2 | 2594.116701398577 | 54527632 | 131072 | 416.0128173828125 |

全5runはSucceeded、全5子processは終了0・Errors空・前後検査成功・前後mask16384。各runのproduction条件比較は差0、rawからの平均/中央値/標準偏差・割当/GC再計算は一致し、通常測定の警告は0だった。scan.simdの3runは元の時間RunVariation5.5%内で成立したが、時間の元2倍モードを今回観測しなかったことは原因解消の証明ではない。

memory.copy-fillの2runは同じ131072操作で総割当が正確に1680bytes異なった。bytes/op差は0.0128173828125で元の1bytes/op幅以内でも、元症状の再出時に止める事前条件に該当するため、5runで停止した。固定期待の本体割当416bytes/opとの差は再出識別だけに使い、測定値からの差引きや補正はしていない。API警告が消えたことと割当不安定性の解消を区別し、具体的な割当thread/型/発生元は未特定のままとする。

10.3はBLOCKED。残り7ゲートrun、全15case×3の基準再特徴化、comparison-policy-recalibrated.jsonの作成と独立レビュー、候補交互6run、production性能比較、タスク13の最終公式全体検証は開始していない。追加取り直し、run選別、閾値緩和、割当指標の変更は行っていない。元policyのSHA-256は`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`のまま、全目標・許容幅・同等範囲・必要独立run数を保持する。再利用は非選定のためRetention/WeakReferenceは非該当、11/12も非選定、Ubuntuは未実施。

全記録は[task-10.3-retry2-20261024T150905Z](../../../artifacts/runtime-performance/task-10.3-retry2-20261024T150905Z/)。`task-brief.md`と`gate-plan.json`に固定条件、`gate-commands.json`に全コマンド/cwd/UTC/終了値、各`inspection.json`にraw再計算・check/mask・操作数/総割当、`gate-result.json`に停止理由を保存した。worker/observerのmaskとPGOはfinallyで復元した。Phase Aの設定保存契約が成立したことは性能受入を意味しない。元の目標・許容幅で判断できる計測条件が確立するまで候補の受入を再開しない。


最終保存照合では旧759記録・再試行1の406記録・672入力・既存2復元先・元文書先頭・両runtime・凍結harness・元policyのhash不一致0を確認した。HEAD/全refs/tasks.mdは不変、初期statusのstage変更0と最終cached diff0を確認した。一方、indexファイルのbyte hashは`91c74761edeb294ef02a8d1383eb60ec7554c04f2044528d0416b2d412c9958d`から`6a46db531bf754cf54d316dc00ff89e9c40986fa0fea9e37dc3a200dd000dc49`へ変わった。初期blob一覧は採取しておらず、byte差の原因は未確定とする。読み取りstatusのstat cache更新とは断定しない。元repoのGit状態を変えるコマンドは実行せず、indexも復元していない。詳細は`index-observations.json`に保存した。

## 10.3再開: 基準3runと版付きpolicy

利用者の指示で、再現しなかったSIMD時間の問題は未解決として記録し、[割り当て測定の修正](allocation-fix.md)を適用した共通harnessで10.3を再開した。記録は[task-10.3-resume-20261025](../../../artifacts/runtime-performance/task-10.3-resume-20261025/)。runtimeや計時区間は変更していない。

旧runtimeの独立3run・45ケースは全Succeeded。全子processの終了0、Errors空、build/validation成功、前後検査と前後mask16384、必須指標と保存hash、rawの平均/中央値/標準偏差・割り当て/GC再計算、production条件差0を確認した。全15ケースのbytes/opが3runで一致し、matrix656、memory416、SIMD448bytes/opだった。SIMD時間は591896.185、615218.242、596998.678ns/opで、約2倍モードの原因解消は確認できないが今回は再現しなかった。

時間のばらつきは残る。Decode+Validate小規模は3818.136、2767.034、2747.133ns/op（S=38.706%）、memory.copy-fillは2604.066、3008.477、2594.198ns/op（S=15.909%）。全15ケースを[numeric-table.md](../../../artifacts/runtime-performance/task-10.3-resume-20261025/numeric-table.md)へ記録し、runを選別していない。基準側だけから9.5の式でNoise/RunVariationを再導出し、削減目標・AllowedIncrease・Equivalence・MinimumIndependentRunsを完全保持した別版policy`a7386a5bc1c7dd07a1483f7359aab5f4aadd51190b4522c8e041988fd93500a1`を保存した。候補データは未使用。原本のpolicy hashは`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`のままである。

baseline-1では、外部observerが終了後もPIDを追跡し、3PIDでmask16384/16777215が混在してworkerの後検査が停止した。測定CLIは終了0で、全15processの内部前後maskは16384。postcheck最終更新より約80〜160秒後の観測と元PIDの終了行を確認したが、旧observerは開始時刻・各mask時刻を残さず、混在値の全発生時点は復元できない。このため`external_observer_verified=false`とし、途中mask無変更は断定せず、production保存契約・内部前後観測を根拠に同runを保持した。元の失敗記録は`baseline-1-observer-failure.json`と`baseline-1-observer-timing.json`にある。observer側だけをPID+開始時刻・名前・pathの同定とpostcheck後の監視停止へ修正し、baseline-2/3は外部観測も成立した。harness・測定値は変更していない。

policyのproduction読込・数値自己比較は30指標Equivalentかつ許容内、対象2指標は同値のためTargetNotReached。CLIは同じrun IDの両群重複を終了1で拒否し、追加の不成立0、保存入力342ファイルのhash不一致0だった。整合確認に合成runやID変更を使っていない。これは候補の受入ではなく、独立レビュー前の基準checkpointである。

Retentionは再利用追加なしのため非該当。runtimeの公開契約検証と修正harnessのRelease警告・エラー0、契約332件成功は既存記録を引き継ぎ、今回新しいテストやフラグは追加していない。候補交互6runは独立レビュー承認後に行い、13.1以降の最終性能・公式受入とは区別する。

### 独立レビュー後の交互6run

基準checkpointは、修正harnessと既存affinity差分の独立レビュー、Releaseビルド・契約332件・45ケース再計算・30指標のpolicy式・整形確認を経てAPPROVEDとなり、親も固定内容を照合した。その後、旧版A1→候補B1→A2→B2→A3→B3を同期間・同条件で直列測定した。全90ケースがSucceededで、全子終了0・Errors空、前後検査・前後mask16384、observerのprocess同定、source固定、時間/割り当て/GCの再計算、production条件差0が成立した。

| 対象 | 基準中央値bytes/op | 候補中央値bytes/op | 削減量bytes/op | 目標 |
| --- | ---: | ---: | ---: | --- |
| loop.integer-direct | 230176 | 744 | 229432 | 180000以上を達成 |
| loop.indirect | 229928 | 496 | 229432 | 180000以上を達成 |

割り当ては両群とも各ケースの3runで一致した。全15ケースの各run値・中央値・絶対差・比率・個別判定は[comparison-values.md](../../../artifacts/runtime-performance/task-10.3-resume-20261025/comparison-values.md)、各runのCI・raw・最小最大は`summary-alternating-baseline/`、`summary-alternating-candidate/`、`comparison/comparison.json`に保存した。

正式compareは**終了1、IsAccepted=false**。両対象の割り当て目標は達成したが、時間2指標のばらつきが事前のRunVariationを超えたため、許容内を確認できたのは28/30指標となった。

| 完了を阻害した時間指標 | A1 / A2 / A3（ns/op） | B1 / B2 / B3（ns/op） | 最大中央値偏差（旧 / 候補） | RunVariation |
| --- | --- | --- | --- | --- |
| invoke.empty | 94.354 / 94.108 / 83.081 | 88.667 / 79.925 / 81.641 | 11.717% / 8.606% | 10.5% |
| kernel.matrix | 6651606.250 / 7023996.205 / 6986647.837 | 6907209.856 / 6620906.490 / 6896216.587 | 4.795% / 3.992% | 3% |

これらは判定不能であり、悪化が確定したケースとは区別する。case判定は改善9・同等3・判定不能3・悪化0。判定不能のうちdecode.largeは時間が改善側で同等範囲外だが許容内のため完了阻害ではなく、invoke.emptyとkernel.matrixの2件だけが比較前提不足で阻害する。条件差は0件だった。

SIMD時間は旧群中央値590450.837ns/op、候補群602740.765ns/op、2.081%増で時間Equivalent。割り当て448→392bytes/opのためcase判定はImprovedだった。約2倍モードは今回も未再現だが、その原因解消は宣言しない。

正式比較の入力684ファイルは前後hash不一致0で、元policyと新版policyを保持した。全6runをそのまま保存し、判定を通すための値の補正・追加取り直し・run選別・閾値変更は行っていない。再実行は`Measure-Alternating.ps1`、保存値の再集計・正式比較は`Verify-And-Compare.ps1`、表示表は`Generate-ComparisonTables.py`を参照する。既存出力を上書きしないよう、新しい記録先を使う。

本再開では両割り当て目標の再現を確認したが、全ケースの比較前提が成立しておらず、第1変更を受入済みとして扱わない。Retentionは引き続き非該当。タスク10.3の性能受入、13.1の別途必要な最終交互比較、13.3/13.4の公式全体受入、Ubuntu/他CPUは未完了・未実施である。
