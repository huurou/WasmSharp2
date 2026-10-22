using BenchmarkDotNet.Attributes;

namespace WasmSharp.Benchmarks.Smoke;

/// <summary>
/// 別processの起動とexporter・割り当て観測を確認するための最小ケース
/// </summary>
public class SmokeBenchmarks
{
    // 空moduleの固定バイナリ 素材コンパイラを試走の前提にしない。
    private readonly byte[] moduleBytes_ = [0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00];
    private WasmModule lastModule_ = null!;

    [GlobalSetup]
    public void Setup()
    {
        WasmModule.Decode(moduleBytes_).Validate().Instantiate(new WasmImports());
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule Decode()
    {
        return lastModule_ = WasmModule.Decode(moduleBytes_);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        lastModule_.Validate().Instantiate(new WasmImports());
    }
}
