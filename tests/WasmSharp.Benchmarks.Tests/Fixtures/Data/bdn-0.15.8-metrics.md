# 固定BDN report fixture

`bdn-0.15.8-metrics.txt`はBenchmarkDotNet0.15.8/.NET10のタスク4.1試走から取り出した測定88行とGC1行。通常テストはこの保存済み出力をBDNの`ExecuteResult`へ渡し、compilerや測定processを起動しない。

- 元の出力:`artifacts/runtime-performance/task-4.1-20261022T222408Z/smoke.log`
- 独立した期待統計:`artifacts/runtime-performance/task-4.1-20261022T222408Z/bdn/results/WasmSharp.Benchmarks.Smoke.SmokeBenchmarks-report-full.json`
- Workload/Resultは15行。平均76.04321956634521ns/op、中央値75.50182342529297ns/op、標準偏差1.3306110771777684ns/op、99.9%信頼区間[74.6207156172511,77.46572351543934]。
- GC行は`// GC: 179 0 0 2818572288 8388608`。割り当て総量2,818,572,288bytes、8,388,608操作、336bytes/op、Gen0は179回で0.021338462829589844回/1,000操作。

元の測定メソッドは空moduleのDecode。保存契約の検証ではdescriptorだけを`InvokeBenchmarks.Single`へ対応付けており、このfixtureをそのケースの性能値として扱わない。素材・入力・期待値・Jobの本体は変更しない。

表示0の検証ではGC行だけを手計算可能な5bytes/20操作へ置換する。丸め前0.25bytes/opに対して表示0を保存し、Gen0/1/2の2/1/1回を100/50/50回/1,000操作として検証する。5bytes/2操作は2.5から2へのToEven丸めを確認する。

1反復の境界はタスク5.3の`final-normal-smoke.log`にある`WorkloadActual 1: 64 op, 43400.00 ns`、`WorkloadResult 1: 64 op, 43100.00 ns`、`// GC: 0 0 0 24576 64`を使う。BDNの信頼区間が非有限になるため、時間統計は理由付きnull、割り当ては384bytes/op、rawは保存する。

統計・丸めの出所は[BenchmarkReport](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Reports/BenchmarkReport.cs)、[Statistics](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Mathematics/Statistics.cs)、[GcStats](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs)。整数表示の丸めは`GcStats.GetBytesAllocatedPerOperation`のToEvenで、GC正規化は[MemoryDiagnoser](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Diagnosers/MemoryDiagnoser.cs)の1,000操作当たり値に合わせる。
