using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 固定素材から毎回新しいmoduleまたはinstanceを生成する準備段階の測定
/// </summary>
public class PreparationBenchmarks
{
    private ImmutableArray<byte> moduleBytes_;
    private BenchmarkProcess? process_;
    private BenchmarkCase definition_ = null!;
    private WasmModule validatedModule_ = null!;
    private WasmImports imports_ = null!;
    private WasmModule lastModule_ = null!;
    private WasmInstance lastInstance_ = null!;

    [GlobalSetup(Target = nameof(DecodeSmall))]
    public void SetupDecodeSmall() => Setup(nameof(DecodeSmall));

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeSmall() => lastModule_ = WasmModule.Decode(moduleBytes_.AsSpan());

    [GlobalSetup(Target = nameof(DecodeLarge))]
    public void SetupDecodeLarge() => Setup(nameof(DecodeLarge));

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeLarge() => lastModule_ = WasmModule.Decode(moduleBytes_.AsSpan());

    [GlobalSetup(Target = nameof(DecodeValidateSmall))]
    public void SetupDecodeValidateSmall() => Setup(nameof(DecodeValidateSmall));

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeValidateSmall() =>
        lastModule_ = WasmModule.Decode(moduleBytes_.AsSpan()).Validate();

    [GlobalSetup(Target = nameof(DecodeValidateLarge))]
    public void SetupDecodeValidateLarge() => Setup(nameof(DecodeValidateLarge));

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmModule DecodeValidateLarge() =>
        lastModule_ = WasmModule.Decode(moduleBytes_.AsSpan()).Validate();

    [GlobalSetup(Target = nameof(InstantiateLarge))]
    public void SetupInstantiateLarge() => Setup(nameof(InstantiateLarge));

    [Benchmark(OperationsPerInvoke = 1)]
    public WasmInstance InstantiateLarge() =>
        lastInstance_ = validatedModule_.Instantiate(imports_);

    [GlobalCleanup]
    public void Cleanup()
    {
        if (process_ is null)
        {
            CheckLastResult();
            return;
        }
        process_.Cleanup(CheckLastResult);
    }

    private void CheckLastResult() =>
        Verify(
            definition_.MeasurementStage == MeasurementStage.Instantiate
                ? new InstanceObservation(lastInstance_)
                : new ModuleObservation(lastModule_)
        );

    internal void Initialize(LoadedAssets assets, string caseId)
    {
        definition_ = assets.Catalog.Get(caseId);
        moduleBytes_ = assets.GetBinary(definition_.AssetId);
        var before = WasmModule.Decode(moduleBytes_.AsSpan());
        if (definition_.MeasurementStage != MeasurementStage.Decode)
        {
            before.Validate();
        }
        Verify(
            definition_.MeasurementStage == MeasurementStage.Instantiate
                ? new InstanceObservation(before.Instantiate(CaseVerifier.CreateImports()))
                : new ModuleObservation(before)
        );

        if (definition_.MeasurementStage == MeasurementStage.Instantiate)
        {
            validatedModule_ = WasmModule.Decode(moduleBytes_.AsSpan()).Validate();
            imports_ = CaseVerifier.CreateImports();
        }
        lastModule_ = null!;
        lastInstance_ = null!;
    }

    private void Setup(string method)
    {
        process_ = BenchmarkProcess.Start(nameof(PreparationBenchmarks) + "." + method);
        process_.Setup(Initialize);
    }

    private void Verify(CaseObservation observation)
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
