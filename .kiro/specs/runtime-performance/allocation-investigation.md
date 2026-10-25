# 1680bytes変動の原因調査

調査日: 2026-10-25。対象はタスク10.3を停止させた、同一操作数での総managed割り当て量の1680bytes差。実装再試行ではなく、利用者の原因調査依頼に基づく診断として実施した。

その後の利用者の修正指示により、測定境界へ対処し、旧ランタイムによる6本の本測定で追加総量0を確認した。[修正内容と検証結果](allocation-fix.md)を参照する。以下は修正前の原因調査の記録として保持する。

## 結論

元のBenchmarkDotNet0.15.8測定で観測した1680bytesの加算は、割り当て計測区間に入るファイナライザーの処理に由来する。元の旧runtime・harness・素材を使う行列ケースで、通常動作とファイナライザー停止だけを交互に切り替え、追加総量が1680→0→1680→0になることを確認した。

具体的な経路は、計測前のGCでキューへ入った`Gen2GcCallback`が`ArrayPool<T>.Shared`の清掃を行うものと判断する。実行版.NET10.0.13のIL、元のBDN子processのヒープ、同じ清掃処理を用いた最小再現が一致している。元の各測定区間について全割り当ての時刻・stackを採取したわけではないため、各1680bytesを構成する全オブジェクトへの帰属は、直接の割り当て履歴ではなく、実装・ヒープ・介入結果を合わせた因果推定である。

SIMD時間の約2倍モードは別に残る未解明事項。同じ保存run群で、時間が約0.599/1.200/0.613ms/opと変化しても割り当てはすべて同じ231056bytesだった。この調査から時間変動まで解消したとは扱わない。

## 発生する仕組み

BDNは時間計測が終わった後、別の追加反復で割り当てを測る。実行版の`Engine.GetExtraStats`と`ForceGcCollect`は次の順序になっている。

```text
GC.Collect()
GC.WaitForPendingFinalizers()
GC.Collect()                         ← このGC後のfinalizer完了は待たない
GcStats.ReadInitial()
WorkloadAction(...)
GcStats.ReadFinal()
```

`Gen2GcCallback`はfinalizerからArrayPoolの`Trim()`を呼び、自身を再登録する。最後の`Collect()`で再び対象となった清掃が、`ReadInitial`の前に終わる場合と、カウンター間に入る場合がある。

`GcStats`は.NET10で`GC.GetTotalAllocatedBytes(precise: true)`を使用する。これはprocess全体のカウンターなので、測定スレッド以外の清掃による割り当ても含む。カウンター間に新たなGen2 GCがなくても、直前のGCに起因する清掃は入り得る。丸め・CSV集計・guest操作数の換算による差ではない。

実行版の`Trim()`には以下の割り当てがある。

| オブジェクト | x64のサイズ | 経路 |
| --- | ---: | --- |
| `GCMemoryInfoData` | 288bytes | `Utilities.GetMemoryPressure`→`GC.GetGCMemoryInfo` |
| `ConditionalWeakTable`の`Enumerator` | 48bytes | poolが追跡するスレッドローカル配列の列挙 |
| 1プールの合計 | 336bytes | 上記2個 |

元のBDN子processには`Char`・`Boolean`・`Object`・`Int32`・`Byte`の5種類のSharedArrayPoolがあり、すべて清掃登録済みだった。5個の`Gen2GcCallback`の弱参照も各poolへ対応した。したがって5回の清掃は`(288 + 48) × 5 = 1680bytes`となる。

測定境界が清掃途中に入る場合は部分量にもなる。最小再現では1プールの清掃による48bytesだけの加算も観測したため、336の倍数だけを検出する一般化はしない。

## 確認結果

すべての新規出力は[診断ディレクトリ](../../../artifacts/runtime-performance/investigate-allocation-20261025/)へ保存した。元の結果や基準値は上書きしていない。

### 1. 保存データの再検出

既存の`Replay-Failure.ps1`を新規出力へ実行し、終了値1、matrix差1680bytes、scan時間比2.004663328879286を確認した。記録は`replayed-failure.json`。

### 2. 秒単位で実行できる最小再現

Wasm・BDNを参照しない`pool-probe`で、同じ.NETのArrayPool清掃とGC順序を使用した。5種類の診断はByte・Char・Int32・Int64・Objectであり、元BDNのBooleanを含む5型そのものではない。直接`Trim`するモードは現在スレッドのカウンターだけを測っており、同モードのJSON内の`ProcessBytes=0`は未計測を表す固定値である。

| 条件 | 反復数 | 観測 |
| --- | ---: | --- |
| 1種類のpoolの`Trim`を直接呼ぶ | 20 | 現在スレッドで毎回336bytes |
| 5種類のpoolの`Trim`を直接呼ぶ | 20 | 現在スレッドで毎回1680bytes |
| `Collect→Wait→Collect`後に測る、5種類 | 20 | processで1680bytesが18回、0bytesが2回。現在スレッドは全件0bytes |
| 最後の`Collect`後にも`Wait`を入れる、5種類 | 20 | process・現在スレッドとも全件0bytes |

別の再現検証コマンドでも、待機なしは20回中15回で1680bytesを検出して終了値1、待機ありは20回すべて0bytesで終了値0だった。実行時間は各約0.9秒。`red-repro.json`と`green-repro.json`に全反復を保存した。

再実行例。出力ファイルは毎回新規名にする。

```powershell
dotnet build artifacts/runtime-performance/investigate-allocation-20261025/pool-probe/PoolProbe.csproj -c Release --nologo
pwsh -File artifacts/runtime-performance/investigate-allocation-20261025/Assert-AllocationRace.ps1 -OutputFile artifacts/runtime-performance/investigate-allocation-20261025/repro-new.json
pwsh -File artifacts/runtime-performance/investigate-allocation-20261025/Assert-AllocationRace.ps1 -OutputFile artifacts/runtime-performance/investigate-allocation-20261025/repro-wait-new.json -WaitForCleanup
```

競合なので各反復の出現率は変わる。未再現の単発実行だけで原因解消と判定しない。

### 3. 元のBDN測定での介入

旧runtimeの固定復元先、既存harness、固定素材、CPU14/mask16384、`DOTNET_TieredPGO=0`を使用。ファイナライザー停止にはBDN内蔵のunit-test専用環境変数を診断時だけ使用し、終了時に元の環境変数とobserverのaffinityを復元した。ソースや計測値は補正していない。

| 実行順 | ファイナライザー | 操作数 | 総割り当て量 | 20992bytesからの差 |
| --- | --- | ---: | ---: | ---: |
| `matrix-stock-1` | 通常 | 32 | 22672bytes | 1680bytes |
| `matrix-blocked-1` | 診断用に停止 | 32 | 20992bytes | 0bytes |
| `matrix-stock-2` | 通常 | 32 | 22672bytes | 1680bytes |
| `matrix-blocked-2` | 診断用に停止 | 32 | 20992bytes | 0bytes |

4runとも終了値0・Succeeded・測定区間のGen2回数0。全runの前後の正しさ確認は成功している。停止条件は時間を計るActual反復の後に適用されるため、この比較は時間変動の原因を検証するものではない。

実行は`Probe-OriginalBdn.ps1`へ`-Label <新規名>`を渡し、停止側だけ`-BlockFinalizer`を追加する。Blockerは専用オブジェクトをfinalizeさせる追加GCを行い、測定中のfinalizerスレッドを停止する仕組みで、単に待機を1回追加する介入ではない。CLIの保存区分は`measurement`だが、隣接する`*-diagnosis.json`で全件を診断用途に明示しており、性能受入へ採用しない。専用環境変数を通常測定の対策として導入しない。

### 4. 元の子processのヒープと実行版IL

`memory.copy-fill`の元BDN子processから別途ヒープを採取した。`original-heap-heap.json`には次がある。

- 上記5種類のSharedArrayPoolと清掃登録フラグ
- 各poolに対応する5個のGen2GcCallbackの弱参照ハンドル
- 288bytesのGCMemoryInfoDataが5個、48bytesのCWT列挙子が5個

これはヒープのsnapshotで、対象測定区間の割り当て履歴ではない。この診断run自身の追加総量は0bytesであり、ヒープ採取は実行タイミングにも影響する。したがって数値比較の4runには混ぜていない。

`heap-reader/runtime-10.0.13-il.json`で、実行版CoreLibの清掃、2種類の生成、callback再登録のILを確認した。CoreLibのProductVersionは`10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`。`heap-reader/benchmarkdotnet-0.15.8-il.json`では実際の旧harnessのBDNとNuGetの同版DLLのhash一致、GC順序、カウンター区間を確認した。v10.0.0の公開ソースだけから実行版の挙動を推定している状態は解消した。

### 5. 再現しなかった条件も保持

- 既存のWasmなしBDN最小probeを今回3launch実行した結果は全件0bytes。過去の0/336/336bytesという結果と区別した。
- 旧harnessのInvokeメソッドを直接呼び、BDNの`ForceGcCollect`を使う`workload-probe`では、memory・matrix・scanの各20反復で追加総量が0bytesだった。元のBDN全体のセットアップとスケジュールを省くと、同じ競合を必ず再現できるわけではない。
- 初期の診断ビルドでaffinity APIのプラットフォーム警告1件を検出し、対応OSの条件を付けて解消した。最終の診断用3projectはReleaseの警告・エラー0。

## 修正する境界と未完了事項

修正の検討対象はBDNの割り当て計測開始直前のGC後処理である。WasmSharpのInvokeや診断情報の遅延生成を変更しても、この背景清掃の競合には対処できない。

最小再現では、最後の`Collect`の後、初期カウンターを読む前に`GC.WaitForPendingFinalizers()`を置くことで混入がなくなった。ただし、これは診断で確認した対処候補であり、元BDNの計測経路への修正と検証は未実施。通常の`GlobalSetup`へ待機を追加するだけでは、その後にBDN自身がGCを行うため必要な境界にならない。上流BDNへの修正や、同じ指標定義を保つ測定基盤側の対応を検討する。

今回、製品コード・既存harness・policy・tasks.mdは変更しておらず、タスク10.3は未完了のまま。時間の約2倍モードも未解明であり、新しい基準値、候補の性能受入、最終公式受入は行っていない。割り当て値から1680bytesを差し引く処置や、許容幅を緩める処置も行っていない。

## 一次資料

- [.NET runtime Issue #101536](https://github.com/dotnet/runtime/issues/101536): .NET8で同種のArrayPool清掃割り当てを説明した報告。今回の結論はこの報告単独ではなく上記の実行版検証に基づく。
- [BDN0.15.8 Engine](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs): GC順序、追加の割り当て計測、unit-test専用のファイナライザー停止。
- [BDN0.15.8 GcStats](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs): process全体のpreciseカウンター。
- [.NET10 SharedArrayPool](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Buffers/SharedArrayPool.cs): 清掃とcallback登録。今回の実行版は上記ILでも確認した。
