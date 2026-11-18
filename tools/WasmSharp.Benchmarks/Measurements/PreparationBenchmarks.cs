using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 固定素材から毎回新しいmoduleまたはinstanceを生成する準備段階の測定
/// </summary>
public class PreparationBenchmarks
{
    private readonly WasmExecutionContext _context = new WasmExecutionContext();
    private CaseJson _json = null!;
    private ImmutableArray<byte> _moduleBytes;
    private BenchmarkProcess? _process;
    private BenchmarkCase _definition = null!;
    private WasmModule _validatedModule = null!;
    private WasmImports _imports = null!;
    private WasmModule _lastModule = null!;
    private WasmInstance _lastInstance = null!;

    [GlobalSetup(Target = nameof(DecodeSmall))]
    public void SetupDecodeSmall()
    {
        Setup(nameof(DecodeSmall));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeSmall()
    {
        return _lastModule = WasmModule.Decode(_moduleBytes.AsSpan());
    }

    [GlobalSetup(Target = nameof(DecodeLarge))]
    public void SetupDecodeLarge()
    {
        Setup(nameof(DecodeLarge));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeLarge()
    {
        return _lastModule = WasmModule.Decode(_moduleBytes.AsSpan());
    }

    [GlobalSetup(Target = nameof(DecodeValidateSmall))]
    public void SetupDecodeValidateSmall()
    {
        Setup(nameof(DecodeValidateSmall));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeValidateSmall()
    {
        return _lastModule = WasmModule.Decode(_moduleBytes.AsSpan()).Validate();
    }

    [GlobalSetup(Target = nameof(DecodeValidateLarge))]
    public void SetupDecodeValidateLarge()
    {
        Setup(nameof(DecodeValidateLarge));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeValidateLarge()
    {
        return _lastModule = WasmModule.Decode(_moduleBytes.AsSpan()).Validate();
    }

    [GlobalSetup(Target = nameof(InstantiateLarge))]
    public void SetupInstantiateLarge()
    {
        Setup(nameof(InstantiateLarge));
    }

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmInstance InstantiateLarge()
    {
        return _lastInstance = _validatedModule.Instantiate(_context, _imports);
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
        Verify(
            _definition.MeasurementStage == MeasurementStage.Instantiate
                ? new InstanceObservation(_lastInstance)
                : new ModuleObservation(_lastModule)
        );
    }

    internal void Initialize(LoadedAssets assets, string caseId, CaseJson json)
    {
        _json = json;
        _definition = assets.Catalog.Get(caseId);
        _moduleBytes = assets.GetBinary(_definition.AssetId);
        var before = WasmModule.Decode(_moduleBytes.AsSpan());
        if (_definition.MeasurementStage != MeasurementStage.Decode)
        {
            before.Validate();
        }
        Verify(
            _definition.MeasurementStage == MeasurementStage.Instantiate
                ? new InstanceObservation(before.Instantiate(_context, CaseVerifier.CreateImports()))
                : new ModuleObservation(before)
        );

        if (_definition.MeasurementStage == MeasurementStage.Instantiate)
        {
            _validatedModule = WasmModule.Decode(_moduleBytes.AsSpan()).Validate();
            _imports = CaseVerifier.CreateImports();
        }
        _lastModule = null!;
        _lastInstance = null!;
    }

    private void Setup(string method)
    {
        _process = BenchmarkProcess.Start(nameof(PreparationBenchmarks) + "." + method);
        _process.Setup(Initialize);
    }

    private void Verify(CaseObservation observation)
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
