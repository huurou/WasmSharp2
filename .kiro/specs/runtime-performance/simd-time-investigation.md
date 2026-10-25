# SIMD時間の約2倍モードの調査

調査日: 2026-10-25。[割り当て測定の修正](allocation-fix.md)を確認した後、過去のscan.simdの約0.6/1.2msという時間差を調査した。

## 現時点の結論と再開判断

原因は未特定。過去の遅い状態は保存データから再検出できるが、今回の修正harness、元harness、SIMD操作だけを呼ぶ短い診断で再現しなかった。元の全15ケース順で実行した場合も、SIMDの生結果は約0.655ms/opだった。未再現を解消の証明とは扱わない。

利用者の「全然再現しなかったら未解決でいいからBlock解除して先に進んでください」に従い、タスク10.3のBlockを解除する。修正済みharnessの基準値再取得から再開し、削減目標・許容増加幅・同等範囲は維持する。診断runを受入用の値へ流用したり、再発した遅いrunを選別して除いたりしない。

## 保存データから確認したこと

- 過去の3runのSIMD平均は598652、1200096、613192ns/op。保存データの再生は終了値1で比2.004663を検出した。
- 約2倍の差はrun全体の一律な低速化ではない。scan.scalarはrun2/run1が1.0082で、他12ケースも概ね0.95〜1.06。memory.copy-fillはrun2が約14%速かった。ただしケースごとに別processなので、SIMD実行中だけの周波数変動やCPU競合までは否定できない。
- Pilotは3runとも約1.6msで、操作数も16→32→64→128→256→512と同一。warmup中にfastは約0.6ms、slowは約1.2msへ分かれた。slowのActual15反復は1.188〜1.240msで、その途中に速度が切り替わった形ではない。
- 3runのGC/JIT環境変数・電源・Job・runtimeconfig・source・素材hashと、全45processのloaded assembly hashは一致した。SIMDの外部affinity観測もすべて16384だった。
- 過去に採取したSIMDのJIT trace6件は全件fastで、slowに直接対応するtraceは存在しない。別のmemoryケースのfast/slow traceはPGO条件も異なり、SIMDの原因確定には使えない。

詳細は[保存データ調査](../../../artifacts/runtime-performance/fix-allocation-20261025/simd-saved-evidence.md)。

## 今回の再現確認

すべて旧runtime・固定素材・CPU14/mask16384・`DOTNET_TieredPGO=0`を使用し、observerはCPU14から外した。診断結果は[調査ディレクトリ](../../../artifacts/runtime-performance/fix-allocation-20261025/)へ保存した。

| 条件 | 回数 | SIMD平均の範囲 | 約2倍モード |
| --- | ---: | ---: | --- |
| 修正harnessの通常Measurement | 3 | 0.591〜0.599ms/op | 未再現 |
| 元harnessの単一ケース、JIT/GC trace付き | 3 | 0.591〜0.627ms/op | 未再現 |
| 同じScanSimdを直接呼ぶ短い診断 | 12process | 0.640〜0.687ms/op | 未再現 |
| 元harnessの全15ケース順、SIMD trace付き | 1 | 約0.655ms/op | 未再現 |

短い診断は2秒warmup後に512操作×8反復し、後半4反復の平均を上表へ使用した。BDNの起動・反復調整を省くため、BDNの性能値との直接比較には使わない。全12processの反復開始・終了CPUは14、GCの観測差は全反復0。CPU使用時間も経過時間に近く、今回の速い状態で大きなスケジューリング待ち時間は見られなかった。

12processのRunLoopはTier1のコードサイズがすべて19674bytesで、アドレス状の定数を正規化した逆アセンブル命令列も一致した。元harnessの新しいtrace4件は欠落イベント0で、RunLoopおよびSIMD関連メソッドのTier1生成をActual開始前に確認した。`simd-trace-summary.json`に時刻・サイズ・段階を保存した。ただし、速い状態同士の一致であり、過去のslow状態が同じコードを実行していたことまでは示さない。

元harnessのSIMD割り当ては、単独3runで追加総量0/1680/1680bytes、全15ケースrunでも1680bytesだった。修正harnessのSIMD3runはいずれも0bytesであり、元の測定競合は今回も再現した。一方、これらの全runのSIMD時間は約0.6msだったため、割り当て混入と約2倍の時間差を同じ現象とは扱わない。

## 仮説と限界

切り分け候補は、①JITの最適化への切り替え時期、②生成コード・配置の差、③実行中のCPU速度・スケジューリング待ち時間の順とした。今回採れた速い状態ではTier1到達、同じRunLoop命令列、CPU位置を確認できた。slowの再現と対応する観測がないため、いずれも過去の原因と断定せず、棄却もしない。

再現した場合は、その同じ子processのJIT/Actualイベント・関連メソッドの生成コード・CPU使用時間を対応付ける必要がある。再実行用の`Probe-FixedBdn.ps1`、`Probe-SimdDirect.ps1`、`Trace-Simd.ps1`とrawを保持する。再現率を確認せず診断用設定を通常測定の恒久対策へ導入しない。

## 再開後の通常測定

[10.3再開の記録](../../../artifacts/runtime-performance/task-10.3-resume-20261025/)でも、旧runtimeの基準3runと旧版・候補版の交互6runを取得した。これら9runのSIMD平均は約0.585〜0.615ms/opで、約2倍モードは再現しなかった。交互比較の旧群中央値590450.837ns/opに対して候補群は602740.765ns/op、2.081%増で、固定policyによる時間判定は同等だった。割り当ては448から392bytes/opへ減った。

同じ比較ではinvoke.emptyとkernel.matrixの時間ばらつきが完了を阻害したため、10.3全体の性能受入は未成立である。これはSIMDの約2倍モードの再発ではない。[保存データの独立診断](../../../artifacts/runtime-performance/debug-10.3-variation-20261025/debug-report.md)でも保存・集計の不具合は見つからず、時間変動そのものの原因は未特定だった。利用者の継続指示に従い、これらの未解決事項を保持して予定済みの最終性能比較と公式機能検証へ進む。
