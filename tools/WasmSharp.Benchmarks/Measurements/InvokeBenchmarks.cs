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
    private readonly WasmExecutionContext _context = new WasmExecutionContext();
    private CaseJson _json = null!;
    private readonly Consumer _consumer = new();
    private BenchmarkProcess? _process;
    private BenchmarkCase _definition = null!;
    private WasmInstance _instance = null!;
    private WasmFunction _function = null!;
    private ImmutableArray<WasmValue> _arguments;
    private WasmResults _lastEmptyResults = null!;
    private WasmResults _lastSingleResults = null!;
    private WasmResults _lastMultiResults = null!;
    private WasmResults _lastIntegerDirectResults = null!;
    private WasmResults _lastFloatHostResults = null!;
    private WasmResults _lastIndirectResults = null!;
    private WasmResults _lastCopyFillResults = null!;
    private WasmResults _lastScanScalarResults = null!;
    private WasmResults _lastScanSimdResults = null!;
    private WasmResults _lastMatrixResults = null!;

    [GlobalSetup(Target = nameof(Empty))]
    public void SetupEmpty()
    {
        Setup(nameof(Empty));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void Empty()
    {
        _lastEmptyResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastEmptyResults);
    }

    [GlobalSetup(Target = nameof(Single))]
    public void SetupSingle()
    {
        Setup(nameof(Single));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void Single()
    {
        _lastSingleResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastSingleResults);
    }

    [GlobalSetup(Target = nameof(Multi))]
    public void SetupMulti()
    {
        Setup(nameof(Multi));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void Multi()
    {
        _lastMultiResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastMultiResults);
    }

    [GlobalSetup(Target = nameof(IntegerDirect))]
    public void SetupIntegerDirect()
    {
        Setup(nameof(IntegerDirect));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void IntegerDirect()
    {
        _lastIntegerDirectResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastIntegerDirectResults);
    }

    [GlobalSetup(Target = nameof(FloatHost))]
    public void SetupFloatHost()
    {
        Setup(nameof(FloatHost));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void FloatHost()
    {
        _lastFloatHostResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastFloatHostResults);
    }

    [GlobalSetup(Target = nameof(Indirect))]
    public void SetupIndirect()
    {
        Setup(nameof(Indirect));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void Indirect()
    {
        _lastIndirectResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastIndirectResults);
    }

    [GlobalSetup(Target = nameof(CopyFill))]
    public void SetupCopyFill()
    {
        Setup(nameof(CopyFill));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void CopyFill()
    {
        _lastCopyFillResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastCopyFillResults);
    }

    [GlobalSetup(Target = nameof(ScanScalar))]
    public void SetupScanScalar()
    {
        Setup(nameof(ScanScalar));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void ScanScalar()
    {
        _lastScanScalarResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastScanScalarResults);
    }

    [GlobalSetup(Target = nameof(ScanSimd))]
    public void SetupScanSimd()
    {
        Setup(nameof(ScanSimd));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void ScanSimd()
    {
        _lastScanSimdResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastScanSimdResults);
    }

    [GlobalSetup(Target = nameof(Matrix))]
    public void SetupMatrix()
    {
        Setup(nameof(Matrix));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public void Matrix()
    {
        _lastMatrixResults = _function.Invoke(_context, _arguments.AsSpan());
        _consumer.Consume(_lastMatrixResults);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_process is null)
        {
            CheckLastResult();
            return;
        }
        _process.Cleanup(CheckLastResult);
    }

    private void CheckLastResult()
    {
        var results = _definition.CaseId switch
        {
            "invoke.empty" => _lastEmptyResults,
            "invoke.single" => _lastSingleResults,
            "invoke.multi" => _lastMultiResults,
            "loop.integer-direct" => _lastIntegerDirectResults,
            "loop.float-host" => _lastFloatHostResults,
            "loop.indirect" => _lastIndirectResults,
            "memory.copy-fill" => _lastCopyFillResults,
            "scan.scalar" => _lastScanScalarResults,
            "scan.simd" => _lastScanSimdResults,
            "kernel.matrix" => _lastMatrixResults,
            _ => throw new InvalidOperationException($"ケース'{_definition.CaseId}'はInvoke測定ではありません。"),
        };
        Verify(new InvokeObservation(_instance, results));
    }

    internal void Initialize(LoadedAssets assets, string caseId, CaseJson json)
    {
        _json = json;
        _definition = assets.Catalog.Get(caseId);
        var input = assets.GetInput(caseId);
        var module = WasmModule.Decode(assets.GetBinary(_definition.AssetId).AsSpan(), WasmSpecVersion.Core2).Validate();
        var before = CreateInstance(module, input);
        Verify(
            new InvokeObservation(
                before,
                before.GetFunction(input.FunctionExport).Invoke(_context, input.Arguments.AsSpan())
            )
        );

        _instance = CreateInstance(module, input);
        _function = _instance.GetFunction(input.FunctionExport);
        _arguments = input.Arguments;
        if (_definition.StatePolicy == StatePolicy.FixedPoint)
        {
            // 計時前に最終状態へ揃え、JIT・pilot・warmup・割り当て観測でも同じ開始条件にする。
            _function.Invoke(_context, _arguments.AsSpan());
        }
        _lastEmptyResults = null!;
        _lastSingleResults = null!;
        _lastMultiResults = null!;
        _lastIntegerDirectResults = null!;
        _lastFloatHostResults = null!;
        _lastIndirectResults = null!;
        _lastCopyFillResults = null!;
        _lastScanScalarResults = null!;
        _lastScanSimdResults = null!;
        _lastMatrixResults = null!;
    }

    private WasmInstance CreateInstance(WasmModule module, PreparedInput input)
    {
        var instance = module.Instantiate(_context, CaseVerifier.CreateImports());
        foreach (var array in input.Arrays)
        {
            instance.GetMemory(array.MemoryExport).Write(array.Offset, array.Bytes.AsSpan());
        }
        return instance;
    }

    private void Setup(string method)
    {
        _process = BenchmarkProcess.Start(nameof(InvokeBenchmarks) + "." + method);
        _process.Setup(Initialize);
    }

    private void Verify(InvokeObservation observation)
    {
        var result = CaseVerifier.Verify(_context, _definition, observation, _json);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{result.CaseId}/{result.Stage}/{result.Subject}: 期待={result.Expected}, 観測={result.Observed}"
            );
        }
    }
}
