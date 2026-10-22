using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseCatalog_ConstructorTests
{
    [Test]
    public async Task 固定15ケースを読む_操作数と仕事量と状態方針をそのまま保持する()
    {
        // Arrange
        var definitions = AssetLoadingFixture.ReadCases();

        // Act
        var catalog = new CaseCatalog(definitions);
        var loop = catalog.Get("loop.integer-direct");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(catalog.Cases.Count()).IsEqualTo(15);
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
        await Assert.That(exception!.Message).Contains("scan.sum-u32").And.Contains(condition);
    }
}
