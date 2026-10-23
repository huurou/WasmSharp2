using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 固定入力による定常Invokeと結果消費を1操作として測定する。
/// </summary>
public class InvokeBenchmarks
{
    private readonly Consumer consumer_ = new();
    private BenchmarkCase definition_ = null!;
    private WasmInstance instance_ = null!;
    private WasmFunction function_ = null!;
    private ImmutableArray<WasmValue> arguments_;
    private WasmResults lastEmptyResults_ = null!;
    private WasmResults lastSingleResults_ = null!;
    private WasmResults lastMultiResults_ = null!;
    private WasmResults lastIntegerDirectResults_ = null!;
    private WasmResults lastFloatHostResults_ = null!;
    private WasmResults lastIndirectResults_ = null!;
    private WasmResults lastCopyFillResults_ = null!;
    private WasmResults lastScanScalarResults_ = null!;
    private WasmResults lastScanSimdResults_ = null!;
    private WasmResults lastMatrixResults_ = null!;

    [GlobalSetup(Target = nameof(Empty))]
    public void SetupEmpty() => Initialize(LoadAssets(), "invoke.empty");

    [Benchmark(OperationsPerInvoke = 1)]
    public void Empty()
    {
        lastEmptyResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastEmptyResults_);
    }

    [GlobalSetup(Target = nameof(Single))]
    public void SetupSingle() => Initialize(LoadAssets(), "invoke.single");

    [Benchmark(OperationsPerInvoke = 1)]
    public void Single()
    {
        lastSingleResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastSingleResults_);
    }

    [GlobalSetup(Target = nameof(Multi))]
    public void SetupMulti() => Initialize(LoadAssets(), "invoke.multi");

    [Benchmark(OperationsPerInvoke = 1)]
    public void Multi()
    {
        lastMultiResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastMultiResults_);
    }

    [GlobalSetup(Target = nameof(IntegerDirect))]
    public void SetupIntegerDirect() => Initialize(LoadAssets(), "loop.integer-direct");

    [Benchmark(OperationsPerInvoke = 1)]
    public void IntegerDirect()
    {
        lastIntegerDirectResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastIntegerDirectResults_);
    }

    [GlobalSetup(Target = nameof(FloatHost))]
    public void SetupFloatHost() => Initialize(LoadAssets(), "loop.float-host");

    [Benchmark(OperationsPerInvoke = 1)]
    public void FloatHost()
    {
        lastFloatHostResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastFloatHostResults_);
    }

    [GlobalSetup(Target = nameof(Indirect))]
    public void SetupIndirect() => Initialize(LoadAssets(), "loop.indirect");

    [Benchmark(OperationsPerInvoke = 1)]
    public void Indirect()
    {
        lastIndirectResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastIndirectResults_);
    }

    [GlobalSetup(Target = nameof(CopyFill))]
    public void SetupCopyFill() => Initialize(LoadAssets(), "memory.copy-fill");

    [Benchmark(OperationsPerInvoke = 1)]
    public void CopyFill()
    {
        lastCopyFillResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastCopyFillResults_);
    }

    [GlobalSetup(Target = nameof(ScanScalar))]
    public void SetupScanScalar() => Initialize(LoadAssets(), "scan.scalar");

    [Benchmark(OperationsPerInvoke = 1)]
    public void ScanScalar()
    {
        lastScanScalarResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastScanScalarResults_);
    }

    [GlobalSetup(Target = nameof(ScanSimd))]
    public void SetupScanSimd() => Initialize(LoadAssets(), "scan.simd");

    [Benchmark(OperationsPerInvoke = 1)]
    public void ScanSimd()
    {
        lastScanSimdResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastScanSimdResults_);
    }

    [GlobalSetup(Target = nameof(Matrix))]
    public void SetupMatrix() => Initialize(LoadAssets(), "kernel.matrix");

    [Benchmark(OperationsPerInvoke = 1)]
    public void Matrix()
    {
        lastMatrixResults_ = function_.Invoke(arguments_.AsSpan());
        consumer_.Consume(lastMatrixResults_);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        var results = definition_.CaseId switch
        {
            "invoke.empty" => lastEmptyResults_,
            "invoke.single" => lastSingleResults_,
            "invoke.multi" => lastMultiResults_,
            "loop.integer-direct" => lastIntegerDirectResults_,
            "loop.float-host" => lastFloatHostResults_,
            "loop.indirect" => lastIndirectResults_,
            "memory.copy-fill" => lastCopyFillResults_,
            "scan.scalar" => lastScanScalarResults_,
            "scan.simd" => lastScanSimdResults_,
            "kernel.matrix" => lastMatrixResults_,
            _ => throw new InvalidOperationException(
                $"ケース'{definition_.CaseId}'はInvoke測定ではありません。"
            ),
        };
        Verify(new InvokeObservation(instance_, results));
    }

    internal void Initialize(LoadedAssets assets, string caseId)
    {
        definition_ = assets.Catalog.Get(caseId);
        var input = assets.GetInput(caseId);
        var module = WasmModule.Decode(assets.GetBinary(definition_.AssetId).AsSpan()).Validate();
        var before = CreateInstance(module, input);
        Verify(
            new InvokeObservation(
                before,
                before.GetFunction(input.FunctionExport).Invoke(input.Arguments.AsSpan())
            )
        );

        instance_ = CreateInstance(module, input);
        function_ = instance_.GetFunction(input.FunctionExport);
        arguments_ = input.Arguments;
        if (definition_.StatePolicy == StatePolicy.FixedPoint)
        {
            // 計時前に最終状態へ揃え、JIT・pilot・warmup・割り当て観測でも同じ開始条件にする。
            function_.Invoke(arguments_.AsSpan());
        }
        lastEmptyResults_ = null!;
        lastSingleResults_ = null!;
        lastMultiResults_ = null!;
        lastIntegerDirectResults_ = null!;
        lastFloatHostResults_ = null!;
        lastIndirectResults_ = null!;
        lastCopyFillResults_ = null!;
        lastScanScalarResults_ = null!;
        lastScanSimdResults_ = null!;
        lastMatrixResults_ = null!;
    }

    private static WasmInstance CreateInstance(WasmModule module, PreparedInput input)
    {
        var instance = module.Instantiate(CaseVerifier.CreateImports());
        foreach (var array in input.Arrays)
        {
            instance.GetMemory(array.MemoryExport).Write(array.Offset, array.Bytes.AsSpan());
        }
        return instance;
    }

    private static LoadedAssets LoadAssets() =>
        AssetManifest.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "asset-manifest.json"));

    private void Verify(InvokeObservation observation)
    {
        var result = CaseVerifier.Verify(definition_, observation);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{result.CaseId}/{result.Stage}/{result.Subject}: 期待={result.Expected}, 観測={result.Observed}"
            );
        }
    }
}
