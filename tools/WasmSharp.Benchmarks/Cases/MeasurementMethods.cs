using System.Collections.Frozen;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定ケースと測定メソッドの対応を保持し、素材の読込前後で同じ対応を使用する
/// </summary>
internal sealed class MeasurementMethods
{
    private readonly FrozenDictionary<string, string> methods_ = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
    {
        ["invoke.empty"] = "InvokeBenchmarks.Empty",
        ["invoke.single"] = "InvokeBenchmarks.Single",
        ["invoke.multi"] = "InvokeBenchmarks.Multi",
        ["loop.integer-direct"] = "InvokeBenchmarks.IntegerDirect",
        ["loop.float-host"] = "InvokeBenchmarks.FloatHost",
        ["loop.indirect"] = "InvokeBenchmarks.Indirect",
        ["memory.copy-fill"] = "InvokeBenchmarks.CopyFill",
        ["scan.scalar"] = "InvokeBenchmarks.ScanScalar",
        ["scan.simd"] = "InvokeBenchmarks.ScanSimd",
        ["kernel.matrix"] = "InvokeBenchmarks.Matrix",
        ["decode.small"] = "PreparationBenchmarks.DecodeSmall",
        ["decode.large"] = "PreparationBenchmarks.DecodeLarge",
        ["decode-validate.small"] = "PreparationBenchmarks.DecodeValidateSmall",
        ["decode-validate.large"] = "PreparationBenchmarks.DecodeValidateLarge",
        ["instantiate.large"] = "PreparationBenchmarks.InstantiateLarge",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    internal bool ContainsCase(string caseId)
    {
        return methods_.ContainsKey(caseId);
    }

    internal string GetMeasurementMethod(string caseId)
    {
        return methods_[caseId];
    }

    internal string GetCaseId(string measurementMethod)
    {
        return methods_.Single(x => x.Value == measurementMethod).Key;
    }
}
