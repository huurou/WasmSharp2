using System.Reflection;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;
using BenchmarkAttribute = BenchmarkDotNet.Attributes.BenchmarkAttribute;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseCatalog_ConstructorTests
{
    [Test]
    public async Task 同じケースIDが重複する_識別子を示して拒否する()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases()[0];

        // Act & Assert
        var exception = await Assert
            .That(() => new CaseCatalog([definition, definition]))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains(definition.CaseId).And.Contains("重複");
    }

    [Test]
    public async Task scalarとSIMDの比較groupが違う_組の不一致を示して拒否する()
    {
        // Arrange
        var definitions = AssetLoadingFixture.ReadCases();
        var scalar = definitions.Single(x => x.CaseId == "scan.scalar");
        var simd = definitions.Single(x => x.CaseId == "scan.simd") with
        {
            ComparisonGroup = "other-group",
        };

        // Act & Assert
        var exception = await Assert
            .That(() => new CaseCatalog([scalar, simd]))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("comparison_group");
    }

    [Test]
    public async Task 固定15ケースを読む_操作数と仕事量と状態方針をそのまま保持する()
    {
        // Arrange
        var definitions = AssetLoadingFixture.ReadCases();
        var measurementMethods = new[] { typeof(InvokeBenchmarks), typeof(PreparationBenchmarks) }
            .SelectMany(x =>
                x.GetMethods()
                    .Where(y => y.GetCustomAttribute<BenchmarkAttribute>() is not null)
                    .Select(y => $"{x.Name}.{y.Name}")
            )
            .ToArray();

        // Act
        var catalog = new CaseCatalog(definitions);
        var loop = catalog.Get("loop.integer-direct");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(catalog.Cases.Count()).IsEqualTo(15);
            foreach (var definition in catalog.Cases)
            {
                var method = catalog.GetMeasurementMethod(definition.CaseId);
                await Assert.That(measurementMethods).Contains(method);
                await Assert.That(CaseCatalog.GetCaseId(method)).IsEqualTo(definition.CaseId);
            }
            await Assert
                .That(
                    catalog
                        .Cases.Select(x => catalog.GetMeasurementMethod(x.CaseId))
                        .Distinct()
                        .Count()
                )
                .IsEqualTo(15);
            await Assert
                .That(
                    catalog.Cases.All(x =>
                        ReferenceEquals(x, definitions.Single(y => y.CaseId == x.CaseId))
                    )
                )
                .IsTrue();
            await Assert.That(loop.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
            await Assert.That(loop.Workload.Single().Count).IsEqualTo(4096UL);
            await Assert
                .That(catalog.Get("memory.copy-fill").StatePolicy)
                .IsEqualTo(StatePolicy.FixedPoint);
            await Assert
                .That(catalog.Get("kernel.matrix").StatePolicy)
                .IsEqualTo(StatePolicy.OverwriteOutput);
            await Assert
                .That(catalog.Get("decode.small").StatePolicy)
                .IsEqualTo(StatePolicy.FreshModule);
            await Assert
                .That(catalog.Get("instantiate.large").StatePolicy)
                .IsEqualTo(StatePolicy.FreshInstance);
            await Assert
                .That(catalog.Get("scan.scalar").InputHash)
                .IsNotEqualTo(catalog.Get("scan.simd").InputHash);
        }
    }

    [Test]
    public async Task 未登録ケースを含む_ケースIDと理由を返す()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases()[0] with
        {
            CaseId = "unknown.case",
        };

        // Act & Assert
        var exception = await Assert
            .That(() => new CaseCatalog([definition]))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("unknown.case").And.Contains("未登録");
    }

    [Test]
    [Arguments("arguments")]
    [Arguments("arrays")]
    [Arguments("workload")]
    [Arguments("expected")]
    [Arguments("operation-count")]
    [Arguments("state-restoration")]
    [Arguments("measurement-stage")]
    [Arguments("state-policy")]
    public async Task scalarとSIMDの比較条件が違う_組と条件名を示して拒否する(string condition)
    {
        // Arrange
        var definitions = AssetLoadingFixture.ReadCases();
        var scalar = definitions.Single(x => x.CaseId == "scan.scalar");
        var simd = definitions.Single(x => x.CaseId == "scan.simd");
        simd = condition switch
        {
            "arguments" => AssetLoadingFixture.WithInput(
                simd,
                simd.Input with
                {
                    Arguments = [new(NumericType.I32, "00000000")],
                }
            ),
            "arrays" => AssetLoadingFixture.WithInput(
                simd,
                simd.Input with
                {
                    Arrays = [simd.Input.Arrays[0] with { Length = 4 }],
                }
            ),
            "workload" => simd with { Workload = [new("scan", 4, WorkloadUnit.Elements)] },
            "operation-count" => simd with
            {
                OperationDefinition = simd.OperationDefinition with { OperationsPerInvoke = 2 },
            },
            "state-restoration" => simd with
            {
                OperationDefinition = simd.OperationDefinition with
                {
                    IncludesStateRestoration = true,
                },
            },
            "measurement-stage" => simd with { MeasurementStage = MeasurementStage.Instantiate },
            "state-policy" => simd with { StatePolicy = StatePolicy.OverwriteOutput },
            _ => simd with
            {
                ExpectedResult = simd.ExpectedResult with
                {
                    Values = [new(NumericType.I32, "00000000")],
                },
            },
        };

        // Act & Assert
        var exception = await Assert
            .That(() => new CaseCatalog([scalar, simd]))
            .Throws<InvalidDataException>();
        var expectedCondition = condition
            is "operation-count"
                or "state-restoration"
                or "measurement-stage"
                or "state-policy"
            ? "operation/state"
            : condition;
        await Assert
            .That(exception!.Message)
            .Contains("scan.sum-u32")
            .And.Contains(expectedCondition);
    }
}
