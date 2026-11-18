using System.Collections.Frozen;
using System.Text.Json;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定ケースの定義を取得し、測定メソッドとの対応と比較条件を確認する。
/// </summary>
internal sealed class CaseCatalog
{
    private readonly MeasurementMethods _methods;
    private readonly FrozenDictionary<string, BenchmarkCase> _definitions;

    internal IEnumerable<BenchmarkCase> Cases => _definitions.Values;

    internal CaseCatalog(IEnumerable<BenchmarkCase> definitions, CaseJson json, MeasurementMethods methods)
    {
        _methods = methods;
        var cases = new Dictionary<string, BenchmarkCase>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!_methods.ContainsCase(definition.CaseId))
            {
                throw new InvalidDataException($"ケース'{definition.CaseId}'は未登録です。");
            }
            if (!cases.TryAdd(definition.CaseId, definition))
            {
                throw new InvalidDataException($"ケース'{definition.CaseId}'が重複しています。");
            }
        }

        _definitions = cases.ToFrozenDictionary(StringComparer.Ordinal);
        if (
            cases.TryGetValue("scan.scalar", out var scalar)
            && cases.TryGetValue("scan.simd", out var simd)
            && scalar.ComparisonGroup != simd.ComparisonGroup
        )
        {
            throw new InvalidDataException("ケース'scan.scalar'と'scan.simd'のcomparison_groupが一致しません。");
        }

        foreach (var group in Cases.GroupBy(x => x.ComparisonGroup, StringComparer.Ordinal))
        {
            var first = group.First();
            foreach (var other in group.Skip(1))
            {
                CheckEqual(first, other, "arguments", first.Input.Arguments.SequenceEqual(other.Input.Arguments));
                CheckEqual(first, other, "arrays", first.Input.Arrays.SequenceEqual(other.Input.Arrays));
                CheckEqual(first, other, "workload", first.Workload.SequenceEqual(other.Workload));
                CheckEqual(
                    first,
                    other,
                    "expected",
                    JsonSerializer.Serialize(first.ExpectedResult, json.Options)
                        == JsonSerializer.Serialize(other.ExpectedResult, json.Options)
                );
                CheckEqual(
                    first,
                    other,
                    "operation/state",
                    first.OperationDefinition.OperationsPerInvoke == other.OperationDefinition.OperationsPerInvoke
                        && first.OperationDefinition.IncludesStateRestoration
                            == other.OperationDefinition.IncludesStateRestoration
                        && first.MeasurementStage == other.MeasurementStage
                        && first.StatePolicy == other.StatePolicy
                );
            }
        }
    }

    internal BenchmarkCase Get(string caseId)
    {
        return _definitions.TryGetValue(caseId, out var definition)
            ? definition
            : throw new InvalidDataException($"ケース'{caseId}'は未登録です。");
    }

    internal string GetMeasurementMethod(string caseId)
    {
        return _methods.GetMeasurementMethod(Get(caseId).CaseId);
    }

    private static void CheckEqual(BenchmarkCase first, BenchmarkCase other, string condition, bool equal)
    {
        if (!equal)
        {
            throw new InvalidDataException(
                $"比較組'{first.ComparisonGroup}'のケース'{first.CaseId}'と'{other.CaseId}'で{condition}が一致しません。"
            );
        }
    }
}
