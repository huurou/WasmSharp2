using BenchmarkDotNet.Attributes;

namespace WasmSharp.Benchmarks.Smoke;

/// <summary>
/// 別processの起動とexporter・割り当て観測を確認するための最小ケース
/// </summary>
public class SmokeBenchmarks
{
    private readonly WasmExecutionContext _context = new WasmExecutionContext();

    // 空moduleの固定バイナリ 素材コンパイラを試走の前提にしない。
    private readonly byte[] _moduleBytes = [0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00];
    private WasmModule _lastModule = null!;

    [GlobalSetup]
    public void Setup()
    {
        WasmModule.Decode(_moduleBytes).Validate().Instantiate(_context, new WasmImports());
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule Decode()
    {
        return _lastModule = WasmModule.Decode(_moduleBytes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _lastModule.Validate().Instantiate(_context, new WasmImports());
    }
}
