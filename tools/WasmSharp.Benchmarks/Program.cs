using BenchmarkDotNet.Running;
using WasmSharp.Benchmarks.Smoke;

BenchmarkSwitcher.FromAssembly(typeof(SmokeConfig).Assembly).Run(args, SmokeConfig.Create());
