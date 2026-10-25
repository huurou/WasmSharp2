# 元の基準revisionと最終版の性能比較結果

2026-10-25のタスク13.1。予定した独立交互6runと全90ケースの測定・保存検証は成功したが、production compareは終了1、`IsAccepted=false`となった。全30指標のうち25指標は許容内で、5ケースの時間が固定した反復間判断幅を超えたため、13.1は未完了・未受入（BLOCKED）とする。悪化判定0件だけでは全体の許容内を確認できたことにならない。

本評価は、利用者の未解決を残して先へ進める指示とtasks.mdの着手順序例外に基づく独立した最終比較である。10.3の不成立run、未完了状態、原因未特定の時間変動を保持し、今回のrunで置き換えない。全件を残して測定を終了し、追加run・再試行・閾値変更・新たな診断ループは行っていない。

## 対象と固定条件

- 基準HEAD: `6fdd7e28d629238306cd5e97960214cafdff8da4`。保存済みの基準runtimeと修正済み共通harnessを組み合わせた比較用ディレクトリを再利用した。
- 候補HEAD: `544e17db97e1fe7be8c697bbb89fb0f375a1fa0a`。未コミット差分を含む最終runtimeを用い、各runのsource記録にHEAD・index/worktree差分・未追跡sourceを保存した。HEADだけで測定対象を同定しない。
- CPU: Intel Core i7-13700K、Windows 10.0.26300、x64、単一論理CPU14（mask16384）。observerはmask16760831。PIDと開始時刻、名前、pathで追跡し、postcheck後は監視対象から外す。
- SDK10.0.402、.NET10.0.13（`10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`）、BDN0.15.8（`1e8238ef6c08e18821edae83ac8d6b0a2bb1bb98`）、Release。`DOTNET_TieredPGO=0`を両側へ指定し、HighPerformance/ACで測定した。
- 全15ケースをA1→B1→A2→B2→A3→B3で直列実行した。各caseは独立processで前後期待値を照合し、標準の準備・pilot・warmup・時間測定を行う。割り当ては時間測定後の追加反復で、最終Actualの操作数とunrollを用いる。計時区間と状態復元は固定case定義どおり。
- `Collect→Wait→Collect→Wait`の修正済みallocation engineを使用した。runtime・harness・素材・元policy・適用policyを今回変更していない。重いビルド・テスト・profileと並行して測定していない。

| 同定対象 | SHA-256 |
| --- | --- |
| 基準runtime集合 | `4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751` |
| 候補runtime集合 | `0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87` |
| 共通harness集合 | `c3f92f2403345611b998a7420dc9054be38c6e190c3618115d3c60170f09fa8f` |
| 元policy | `e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba` |
| 適用policy | `a7386a5bc1c7dd07a1483f7359aab5f4aadd51190b4522c8e041988fd93500a1` |
| 素材manifest | `b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a` |

[元policy](comparison-policy.json)の許容増加幅・同等範囲・削減目標・必要run数を保持した[再導出版](comparison-policy-recalibrated.json)を全体比較にも固定適用した。設計「基準値と改善計画」に従い、harness修正後の基準側再測定からノイズ幅と反復間判断幅だけを再導出し、独立レビューを受けた既存版である。今回は再導出していない。複製したpolicy-diff.jsonのレビュー待ち表示は過去の導出時点の記録であり、今回の承認状態を示さない。

## 独立runの対応

| label | run ID | 開始UTC | 完了UTC | 結果 |
| --- | --- | --- | --- | --- |
| A1 | `46a34458379f4cbd947546002fa4331f` | 01:04:18 | 01:07:44 | 15/15成功 |
| B1 | `927649e47cdc4b40a0c7bdb63d6f63f6` | 01:07:48 | 01:11:14 | 15/15成功 |
| A2 | `cedf3aa9941d4cfc8ca752f9533a619f` | 01:11:17 | 01:14:36 | 15/15成功 |
| B2 | `2c010126e2454f3b86af48154ace9e70` | 01:14:38 | 01:18:08 | 15/15成功 |
| A3 | `b3d3f503870d412a8c34431f0f5eb7aa` | 01:18:10 | 01:22:05 | 15/15成功 |
| B3 | `faad0327a8664b87bab25759c705a8f0` | 01:22:07 | 01:25:33 | 15/15成功 |

6runすべてでprocess終了0、前後期待値成功、内部前後mask16384、外部observer成立、警告0、raw時間・割り当て・GCの再計算一致を確認した。source集合は開始前・各run前後・全体終了後で一致し、各runのcollector assemblyは前後不変。子processが読み込んだassemblyのpath/hashも保存した。collector用とBDNが生成した子process用のassemblyは区別し、source集合とproduction比較で同一条件を確認した。

## 全15ケースの比較

時間は各独立runの平均ns/opの中央値、割り当ては丸め前bytes/opの中央値。差は候補−基準である。時間判定が「判定不能」でも、許容内と確認できた同等範囲外の場合は完了を阻害しない。`invoke.single`と`invoke.multi`がこれに該当する。

| case | 時間ns/op: 基準→候補 | 時間変化率 | 時間判定 | 割り当てbytes/op: 基準→候補（差） | 割当判定 | case判定 | 完了阻害 |
| --- | ---: | ---: | --- | ---: | --- | --- | --- |
| `decode-validate.large` | 163,126.755→164,940.516 | +1.112% | 同等 | 723,696→723,696（+0） | 同等 | 同等 | なし |
| `decode-validate.small` | 2,824.522→2,779.724 | -1.586% | 同等 | 9,144→9,144（+0） | 同等 | 同等 | なし |
| `decode.large` | 57,111.967→58,383.545 | +2.226% | 同等 | 360,496→360,496（+0） | 同等 | 同等 | なし |
| `decode.small` | 943.431→954.108 | +1.132% | 同等 | 4,328→4,328（+0） | 同等 | 同等 | なし |
| `instantiate.large` | 5,885.767→5,995.562 | +1.865% | 同等 | 78,720→78,664（-56） | 改善 | 改善 | なし |
| `invoke.empty` | 85.604→80.930 | -5.460% | 同等 | 184→128（-56） | 改善 | 改善 | なし |
| `invoke.multi` | 174.001→168.502 | -3.160% | 判定不能 | 640→584（-56） | 改善 | 改善 | なし |
| `invoke.single` | 126.142→121.161 | -3.948% | 判定不能 | 384→328（-56） | 改善 | 改善 | なし |
| `kernel.matrix` | 6,661,627.404→6,688,707.933 | +0.407% | 判定不能 | 656→600（-56） | 改善 | 判定不能 | あり |
| `loop.float-host` | 773,101.901→783,237.279 | +1.311% | 判定不能 | 557,504→557,448（-56） | 改善 | 判定不能 | あり |
| `loop.indirect` | 903,260.686→897,984.916 | -0.584% | 同等 | 229,928→496（-229,432） | 改善 | 改善 | なし |
| `loop.integer-direct` | 1,038,077.232→1,004,141.964 | -3.269% | 判定不能 | 230,176→744（-229,432） | 改善 | 判定不能 | あり |
| `memory.copy-fill` | 2,960.285→5,097.601 | +72.200% | 判定不能 | 416→360（-56） | 改善 | 判定不能 | あり |
| `scan.scalar` | 2,309,732.254→2,314,518.930 | +0.207% | 判定不能 | 448→392（-56） | 改善 | 判定不能 | あり |
| `scan.simd` | 616,318.034→616,811.286 | +0.080% | 同等 | 448→392（-56） | 改善 | 改善 | なし |

case判定は改善6件、同等4件、悪化0件、判定不能5件。許容内は時間10/15、割り当て15/15、計25/30指標。条件差は0件。個別値・絶対差・比率・理由は[全30指標の表](../../../artifacts/runtime-performance/task-13.1-20261025/comparison-values.md)、全runの値・最小最大・99.9%CIは[production比較](../../../artifacts/runtime-performance/task-13.1-20261025/comparison/comparison.json)と群別summaryに保存した。CIはrun内部のBDN統計であり、run間変動や群中央値の信頼区間ではない。

## 事前目標と未成立の理由

`loop.indirect`と`loop.integer-direct`の割り当て削減は3組とも229432bytes/opで、事前180000bytes/op目標と基準ノイズ幅を超えた。`loop.indirect`は時間同等かつ両指標許容内の改善となり、少なくとも1件の目標再現は成立した。`loop.integer-direct`は割り当て目標を満たす一方、候補側の時間ばらつきが上限を超え、case全体は判定不能である。全体受入には全ケースの許容内の確認も必要であり、目標達成だけで13.1を完了にはしない。

| case | 基準群の中央値からの最大偏差 | 候補群の中央値からの最大偏差 | 固定RunVariation |
| --- | ---: | ---: | ---: |
| `kernel.matrix` | 4.7752% | 4.4243% | 3% |
| `loop.float-host` | 5.2948% | 36.7604% | 10% |
| `loop.integer-direct` | 4.2843% | 39.5023% | 5.5% |
| `memory.copy-fill` | 66.0753% | 50.3690% | 17.5% |
| `scan.scalar` | 40.7097% | 4.0653% | 4% |

この5ケースでは中央値の増減だけを使って許容内とせず、productionのばらつき判定を保持した。これは10.3で未解決となった同版process間の時間変動と同じ種類の制約であり、今回も物理的・実装上の原因は特定していない。生値・保存値・比較計算の不一致や条件差は観測していない。追加測定や閾値緩和によって合格へ置き換えない。

`memory.copy-fill`は時間中央値が約2960→5098ns/op（+72.2%）で、A3/B1/B3には約4.9〜5.2μsの高い値がある。観測上の大きな増加を保持し、悪化判定0件を「悪化がない」という意味には用いない。基準・候補の両群が反復間判断幅を超えているため、正式な悪化判定に進む前提が成立しておらず、累積悪化がないことも確認できていない。

`scan.simd`の時間はA1/A2/A3=616,318.034, 631,485.238, 610,506.306ns/op、B1/B2/B3=616,811.286, 614,048.633, 624,954.353ns/op。中央値は616,318.034→616,811.286ns/op（+0.080%）で時間同等、割り当ては448→392bytes/opでcase判定は改善。約2倍の時間モードは今回の6runでも再現せず、原因解消とは扱わない。

managed割り当ては追加反復で観測したprocess全体の累積量を操作数で割った値であり、保持量・ピーク量・nativeメモリ・guest線形メモリの利用量ではない。GCは同じ追加反復の総回数・操作数・1000操作当たりの値として保存した。再利用を追加した変更ではないため、このタスクで保持観測を追加する対象はない。

## 再集計と保存先

- [Task Brief](../../../artifacts/runtime-performance/task-13.1-20261025/task-brief.md)、[実行計画](../../../artifacts/runtime-performance/task-13.1-20261025/alternating-plan.json)、[最終確認](../../../artifacts/runtime-performance/task-13.1-20261025/final-check.json)、[比較検証](../../../artifacts/runtime-performance/task-13.1-20261025/verification.json)。
- [基準群summary](../../../artifacts/runtime-performance/task-13.1-20261025/summary-alternating-baseline/summary.json)、[候補群summary](../../../artifacts/runtime-performance/task-13.1-20261025/summary-alternating-candidate/summary.json)、各群のmeasurements.csvに全runの統計・CI・割り当て総量・操作数・GCを保持。
- [正式比較コマンド](../../../artifacts/runtime-performance/task-13.1-20261025/compare.command.json)、[終了値](../../../artifacts/runtime-performance/task-13.1-20261025/compare.exit.txt)、[入力hash一覧](../../../artifacts/runtime-performance/task-13.1-20261025/input-before.json)。比較前後で684ファイルのhash不一致0件。
- [測定停止確認](../../../artifacts/runtime-performance/task-13.1-20261025/measurement-stopped.json)、[source開始前](../../../artifacts/runtime-performance/task-13.1-20261025/source-before.json)、[source終了後](../../../artifacts/runtime-performance/task-13.1-20261025/source-final.json)。各runの`*-command.json`、`*-inspection.json`、`checks/`、`raw/`にも詳細を保持。
- 実行は`pwsh -NoProfile -File artifacts/runtime-performance/task-13.1-20261025/Run-Measurement.ps1`。保存結果の照合・比較は同ディレクトリの`Verify-And-Compare.ps1`、表示用表は`python Generate-ComparisonTables.py`で行った。スクリプトは既存出力を上書きせず、再実行時は新しい記録先が必要。今回、追加測定を予定・実施していない。

## Verification Result

- STATUS: NOT_VERIFIED
- CLAIM_TYPE: TASK
- CLAIM: タスク13.1の最終性能受入が成立し、全30指標を許容内と判断できる。
- EVIDENCE: 新規独立6run/90ケースと各run検査は終了0。群別保存契約の再集計は両側終了0。production compareは終了1、IsAccepted=false、許容内25/30、条件差0、ばらつきによる完了阻害5件。direct/indirectの割り当て削減は各229432bytes/op。
- GAPS: 5ケースの時間の許容内を判定できず、要件6.7と設計「完了条件」を満たさない。新規テストや機能フラグは計測・保存・文書化だけのタスクには非該当。既存のビルド・単体テスト結果を今回の新規検証として数えない。
- NOTES: 13.1と10.3の未完了・未受入を保持する。親controllerは順序例外に従って13.3の独立機能検証へ進められるが、13.4の受入後baseline保存と仕様全体の完了は宣言しない。要件6.4、6.5、6.6、6.7、6.8と設計「成否と保存」「比較」「指標と集計単位」「基準値と改善計画」「完了条件」を確認した。
