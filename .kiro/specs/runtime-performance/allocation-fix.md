# 割り当て測定のGC後処理の修正

2026-10-25の利用者の修正指示に基づき、[原因調査](allocation-investigation.md)で特定した準備GCとArrayPool清掃の競合へ対処した。Wasmランタイムと比較policyは変更していない。

## 変更

`WasmAllocationEngineFactory`と内部`AllocationEngine`を測定基盤へ追加し、MeasurementとSmokeから使用する。BDNの公開拡張点を使い、時間計測・自動調整・生時間結果・outlier処理は標準エンジンへ委譲する。

追加統計では、最後のActual反復の操作数と標準エンジンの有効unrollを使い、初期カウンターの直前を`Collect→Wait→Collect→Wait`とした。process全体の`GcStats`を維持し、固定量の減算やスレッド単位への置換は行わない。IterationSetup/Cleanup、ThreadingStats、例外頻度と失敗時の購読解除・後片付けも保持する。

標準エンジンの`BenchmarkStop`イベントは追加統計より前になる。通常の保存指標は取得されるが、外部トレースではprocess終了まで採取する必要がある。READMEとdesignへこの制約と拡張範囲を記載した。

## 検証

記録は[今回の検証出力](../../../artifacts/runtime-performance/fix-allocation-20261025/)へ保存した。

- 修正前の最小再現は20回中15回で1680bytesを検出して終了値1、最後の待機を加えた同じ再現は20回すべて0bytes、終了値0だった。元のBDNによる通常/ファイナライザー停止の交互4runでも1680/0/1680/0を確認済み。
- 設定・委譲・操作数・例外頻度・Smoke接続でRED→GREENを確認。Releaseソリューションビルドは警告・エラー0、測定ツールの契約テスト332件成功、失敗・スキップ0。独立レビューでも332件成功、変更9コードファイルの整形確認成功、`APPROVED`。
- 固定素材の全15ケースの確認に成功。旧ランタイムへ測定側の修正だけを適用した新しいコピーで、別processのSmokeと本測定を実行した。

| 本測定 | 独立run数 | 1runの操作数 | 各runの総割り当て | bytes/op | 追加総量 |
| --- | ---: | ---: | ---: | ---: | ---: |
| kernel.matrix | 3 | 32 | 20992bytes | 656 | 全件0bytes |
| memory.copy-fill | 3 | 131072 | 54525952bytes | 416 | 全件0bytes |
| scan.simd | 3 | 512 | 229376bytes | 448 | 全件0bytes |

Smokeのmatrixも64操作・41984bytesで追加総量0。最初の6本と、続くSIMD調査の3本は終了値0、Succeeded、前後検査成功、CPU14/mask16384、raw GC行とCSV再計算一致、同ケースの条件差0。BDNのunit-test専用ファイナライザーブロッカーは使用していない。`allocation-verification.json`と各`*-inspection.json`に結果を保持した。

元ランタイムの集合hashは`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`、修正harnessは`c3f92f2403345611b998a7420dc9054be38c6e190c3618115d3c60170f09fa8f`。作業先の候補ランタイムは`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`のままで、policy・素材・SDK/runtime完全版も前後一致を確認した。

## 判定の範囲

元の1680bytes混入を対象とする修正を確認した。workload自身のGCや背景処理が発生させる割り当てまで除外する変更ではない。今回の測定は修正検証・診断用であり、新しい基準値や性能改善の受入へ採用しない。SIMD時間の約2倍モードは別の調査対象で、タスク10.3と最終性能受入は未完了。
