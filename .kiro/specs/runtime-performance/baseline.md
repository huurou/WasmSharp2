# 性能基準値

タスク9.2で、同版・同じ6素材/15ケース・同条件のMeasurementを独立3run取得した。記録日は2026-10-24。時間とmanaged割り当て量を同格の主指標とし、以下のrunを基準値の計算に用いる。Smokeと診断用runは含めていない。改善の数値目標、許容幅、ComparisonPolicyは後続のタスク9.5で定める。

## 基準版と固定条件

| 項目 | 値 |
| --- | --- |
| 保存元HEAD | `6fdd7e28d629238306cd5e97960214cafdff8da4` |
| runtime集合SHA-256 | `4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751` |
| harness集合SHA-256 | `bfdd7e94b71ecbf21c50669310920af24dd10616861790f5d668b926609c824d` |
| 採用素材manifest SHA-256 | `b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a` |
| cases.json SHA-256 | `bf0a93272f03e4bbb0717634724f39cc6d0f2169bc9fb84825614cf566ad0389` |
| CPU | 13th Gen Intel(R) Core(TM) i7-13700K、24論理プロセッサ |
| OS/architecture | Microsoft Windows 10.0.26300、OS/processともX64 |
| SDK | `10.0.402` |
| runtime完全版 | `.NET 10.0.13; 10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699` |
| BenchmarkDotNet完全版 | `0.15.8+1e8238ef6c08e18821edae83ac8d6b0a2bb1bb98` |
| ビルド/Job | Release、Measurement、Throughput、1launch、.NET10、X64 |
| GC/JIT | Concurrent Workstation GC、RyuJIT。Job/runtimeconfig/関連環境変数は各runに保存 |
| 電源 | 高パフォーマンス`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`、AC接続 |

採用素材は`artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json`。再生成や入力・期待値の更新は行っていない。測定cwdは`C:/Users/taihe/AppData/Local/Temp/wasmsharp-baseline-9.1-10c52c20ac284cf89b401fce851df9a6/`で、保存HEADと差分を反映した独立復元版を使った。各runの`source_file`からHEAD、空のindex差分、README変更のみのworktree差分、runtime/harnessのファイル一覧とassemblyの同定を辿れる。途中のsource/project/assembly変更は全runで0件。

計時・検査・集計harnessとproject設定・依存版は固定した。事前に使用source/assemblyとSDK/runtime完全版を照合し、全15ケースの`check`が終了0で成功した。測定中に別のbuild/test/profileを並行実行していない。電源planは変更していない。TieredCompilation/TieredPGO/ReadyToRunの内部実効値は公開APIから取得できないため理由付きUnavailableを保持し、取得したJob/runtimeconfig/環境変数を条件照合に用いた。

## 採用runと保存状態

記録の基点は[`artifacts/runtime-performance/task-9.2-20261024T104127Z/`](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/)。時刻は2026-10-24のUTC。

| 保存先 | run ID | run.jsonのSHA-256 | 開始〜完了 |
| --- | --- | --- | --- |
| [run-1](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/run-1/run.json) | `3206fba50e614febb8885410b7a05b13` | `535717bd4afd703c70a7264fb5a273cebf4db1565567a20c7ad11d6e6bc3f9a2` | 10:42:21〜10:47:45 |
| [run-2](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/run-2/run.json) | `57909a586a1644c0a00874fc932624b2` | `a4d48dae26184f15f189f7c8353e0c7c216982dd0ecdd188b6169dec490e73a0` | 10:48:10〜10:53:22 |
| [run-3](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/run-3/run.json) | `9340450da37b4a29896e076fc71e3b9c` | `aa696bd50dc8f35fd0ba00bdc7dd93aab3338321e0de8f513a0cd72db6c01c9e` | 10:53:54〜10:59:25 |

各runは新しいID/出力先で直列起動し、親終了0、用途Measurement、15/15ケースSucceededで保存完了した。失敗・Unavailable・Incomplete・予定ケース欠落は0件。各ケースのbuild/validation成功、子終了0、Errors空、結果行、同一case/Job/PIDの前検査・実行開始・後検査成功、時間・丸め前割り当て・GC・raw・保存参照hashを照合した。子が読み込んだruntime/harness assemblyのpath/hashも保存している。

`Summarize-Baseline.ps1`は既存の`RunStore.Load`と`RunConditions.Create`を呼び、全45case/runの14,826条件値を先頭runの同じcaseと照合し、差0を確認した。各runのraw Result行から再計算した平均・中央値と測定数も一致した。各run内のResult反復数は12〜36回で、pilot/warmup/反復数/操作数の自動調整はBDN既定のままとした。

`run-*.command.json`/`run-*.log`/`run-*.exit.txt`にコマンド・cwd・終了値を保存した。`summary/summary.json`には採用ID/hash、環境/source/素材/Job/raw/検査の参照と各runの平均・中央値・標準偏差・99.9%CI・反復数、割当総量/操作数/丸め前bytes/op、GCを保持する。`summary/conditions-reference.json`は条件値、`summary/measurements.csv`は45件の各run統計を再確認するための一覧。元のFull JSON、measurement CSV、BDNログ、process検査snapshotは各run内に保持した。

## 15ケースの基準値と観測幅

時間の代表値は独立runの平均ns/opの中央値、割り当ての代表値は独立runの丸め前bytes/opの中央値。時間の「幅/中央値」は`(最大−最小)/中央値`で、今回観測した幅を示す。時間は小数点以下3桁へ表示を丸め、丸め前の値は保存データに残す。割り当ての範囲は丸め前の値を表示する。

| case ID | 時間の中央値[最小,最大]ns/op | 時間の幅/中央値 | 割り当ての中央値[最小,最大]bytes/op |
| --- | ---: | ---: | ---: |
| `decode-validate.large` | 154,499.470[152,018.741,161,909.404] | 6.402% | 723600[723600,723600] |
| `decode-validate.small` | 2,301.340[2,271.723,2,312.730] | 1.782% | 9112[9112,9112] |
| `decode.large` | 53,747.967[53,043.997,55,477.815] | 4.528% | 360496[360496,360496] |
| `decode.small` | 809.104[794.452,830.125] | 4.409% | 4328[4328,4328] |
| `instantiate.large` | 6,240.249[6,122.744,6,256.805] | 2.148% | 78720[78720,78720] |
| `invoke.empty` | 52.314[50.390,54.088] | 7.070% | 184[184,184] |
| `invoke.multi` | 316.695[314.702,318.342] | 1.149% | 640[640,640] |
| `invoke.single` | 275.607[275.248,276.547] | 0.471% | 384[384,384.00001525878906] |
| `kernel.matrix` | 4,601,953.348[4,454,080.971,4,672,083.229] | 4.737% | 656[656,656] |
| `loop.float-host` | 1,246,765.081[1,245,940.820,1,295,562.435] | 3.980% | 557504[557504,557504.0625] |
| `loop.indirect` | 1,033,469.095[1,016,128.155,1,039,828.132] | 2.293% | 229928[229928,229928.0625] |
| `loop.integer-direct` | 1,101,456.452[1,092,462.305,1,113,145.787] | 1.878% | 230176[230176,230176] |
| `memory.copy-fill` | 2,561.699[2,531.978,2,562.727] | 1.200% | 416[416,416.0001220703125] |
| `scan.scalar` | 1,637,723.513[1,557,653.294,1,686,816.758] | 7.887% | 448[448,448] |
| `scan.simd` | 422,562.943[403,987.492,431,743.784] | 6.569% | 448[448,448] |

時間の幅/中央値は0.471〜7.887%を観測した。11ケースの割り当て量は3runで一致し、残る4ケースは表の小さな差を観測した。割り当ての最大相対幅は約0.000029344%で、表示値の整数丸めで差を消していない。これらはこの3runの観測であり、将来の上限や許容幅の保証ではない。後続の改善計画ではrun間差も根拠にし、run内CIだけで判断幅を決めない。

各ケースの1操作は固定case定義に従う1回の公開操作。準備系は読み込み済みbyte列から毎回新しいmoduleをDecode/Decode+Validateし、Instantiateは検証済みmoduleから毎回新しいinstanceを生成する。Invoke系は新しいbenchmark process内でmodule/instance/export/引数を計時外に準備し、公開Invokeから結果の取得・消費までを測る。JIT/pilot/warmup/本測定の反復と、割り当て/GCの追加反復を区別し、可変状態の扱いは保存したcase定義に従う。

時間はoverhead控除・RemoveUpper処理後のWorkload/Resultのバッチ平均に対する統計で、個々のInvokeの遅延分布を表さない。99.9%CIは各run内の統計で、独立run間の差のCIではない。割り当ては`GcStats.GetTotalAllocatedBytes(false)/TotalOperations`で得たprocess全体のmanaged累積量で、時間とは別の追加反復に対応する。GCは同区間のGen0/1/2総回数・操作数・1,000操作当たりを保存した。保持量・ピーク量・nativeメモリ・既存guest線形メモリ内の利用量はこの値から判断しない。

## 再実行と再集計

新しい本測定には[測定ツールREADMEの復元・本測定手順](../../../tools/WasmSharp.Benchmarks/README.md#保存した基準revisionの復元)を使い、同じ完全版・素材・固定harness・環境条件で、新規出力先へ1runずつ実行する。この記録では次のコマンドを復元先cwdから使った。

```powershell
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets D:/source/repos/WasmSharp2/artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json --case all --kind measurement --output D:/source/repos/WasmSharp2/artifacts/runtime-performance/task-9.2-20261024T104127Z/run-1
```

上記の`run-1`は採用済みなので再使用しない。出力先を未作成の新規pathへ替える。保存データだけの再集計は、PowerShell7.5以上でリポジトリルートから行う。測定を再起動せず、入力runは変更しない。

```powershell
$record = (Resolve-Path 'artifacts/runtime-performance/task-9.2-20261024T104127Z').Path
$runs = @(1..3 | ForEach-Object { Join-Path $record "run-$_" })
$assembly = 'C:/Users/taihe/AppData/Local/Temp/wasmsharp-baseline-9.1-10c52c20ac284cf89b401fce851df9a6/tools/WasmSharp.Benchmarks/bin/Release/net10.0/WasmSharp.Benchmarks.dll'
$assets = (Resolve-Path 'artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json').Path
$output = Join-Path $record ('summary-' + [guid]::NewGuid().ToString('N'))
& (Join-Path $record 'Summarize-Baseline.ps1') -RunDirectories $runs -OutputDirectory $output -BenchmarkAssemblyPath $assembly -AssetManifestPath $assets
```

固定復元先がない場合は、READMEの復元手順へ採用`run-1`を渡し、記録した完全版でReleaseビルドしたassemblyのpathを`$assembly`へ指定する。集計scriptは計時projectに含めず、採用IDの重複、Smokeや失敗/未完了run、必須指標欠落、source/hash/条件不一致を拒否する。

同じ3runを別出力先へ再集計し、`measurements.csv`のSHA-256が両方とも`9a5ee71bac4b17c3e1ec544efe8dd10a86bd537498aaf8de96381c6046ad23f8`で一致した。詳細は`summary-final.command.json`/`summary-final.log`/`summary-final.exit.txt`、`summary-recheck.*`、`reaggregation-check.json`を参照する。

## 未実施範囲

今回は基準側の同版再測定と保存・集計を行った。候補版との交互測定、原因調査/profile、数値目標と受入policy、最適化、保持メモリ観測、最終の公式全体run/比較/verify、Ubuntuは未実施。C#・CLI・計時harness・依存版は変更していないため、通常契約fixtureへ本測定起動を追加せず、新たな単体テスト/回帰テストの実行も行っていない。全15ケースの今回の測定成功は、性能改善や仕様全体の受入完了を示すものではない。
