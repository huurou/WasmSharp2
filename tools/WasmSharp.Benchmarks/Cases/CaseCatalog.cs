using System.Collections.Frozen;
using System.Text.Json;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定ケースの定義を取得し、測定メソッドとの対応と比較条件を確認する。
/// </summary>
internal sealed class CaseCatalog
{
    private static readonly FrozenDictionary<string, string> methods_ = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
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

    private readonly FrozenDictionary<string, BenchmarkCase> definitions_;

    internal IEnumerable<BenchmarkCase> Cases => definitions_.Values;

    internal CaseCatalog(IEnumerable<BenchmarkCase> definitions)
    {
        var cases = new Dictionary<string, BenchmarkCase>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!methods_.ContainsKey(definition.CaseId))
            {
                throw new InvalidDataException($"ケース'{definition.CaseId}'は未登録です。");
            }
            if (!cases.TryAdd(definition.CaseId, definition))
            {
                throw new InvalidDataException($"ケース'{definition.CaseId}'が重複しています。");
            }
        }

        definitions_ = cases.ToFrozenDictionary(StringComparer.Ordinal);
        if (
            cases.TryGetValue("scan.scalar", out var scalar)
            && cases.TryGetValue("scan.simd", out var simd)
            && scalar.ComparisonGroup != simd.ComparisonGroup
        )
        {
            throw new InvalidDataException(
                "ケース'scan.scalar'と'scan.simd'のcomparison_groupが一致しません。"
            );
        }

        foreach (var group in Cases.GroupBy(x => x.ComparisonGroup, StringComparer.Ordinal))
        {
            var first = group.First();
            foreach (var other in group.Skip(1))
            {
                CheckEqual(
                    first,
                    other,
                    "arguments",
                    first.Input.Arguments.SequenceEqual(other.Input.Arguments)
                );
                CheckEqual(
                    first,
                    other,
                    "arrays",
                    first.Input.Arrays.SequenceEqual(other.Input.Arrays)
                );
                CheckEqual(first, other, "workload", first.Workload.SequenceEqual(other.Workload));
                CheckEqual(
                    first,
                    other,
                    "expected",
                    JsonSerializer.Serialize(first.ExpectedResult, CaseJson.Options)
                        == JsonSerializer.Serialize(other.ExpectedResult, CaseJson.Options)
                );
                CheckEqual(
                    first,
                    other,
                    "operation/state",
                    first.OperationDefinition.OperationsPerInvoke
                        == other.OperationDefinition.OperationsPerInvoke
                        && first.OperationDefinition.IncludesStateRestoration
                            == other.OperationDefinition.IncludesStateRestoration
                        && first.MeasurementStage == other.MeasurementStage
                        && first.StatePolicy == other.StatePolicy
                );
            }
        }
    }

    internal BenchmarkCase Get(string caseId) =>
        definitions_.TryGetValue(caseId, out var definition)
            ? definition
            : throw new InvalidDataException($"ケース'{caseId}'は未登録です。");

    internal string GetMeasurementMethod(string caseId) => methods_[Get(caseId).CaseId];

    private static void CheckEqual(
        BenchmarkCase first,
        BenchmarkCase other,
        string condition,
        bool equal
    )
    {
        if (!equal)
        {
            throw new InvalidDataException(
                $"比較組'{first.ComparisonGroup}'のケース'{first.CaseId}'と'{other.CaseId}'で{condition}が一致しません。"
            );
        }
    }
}
