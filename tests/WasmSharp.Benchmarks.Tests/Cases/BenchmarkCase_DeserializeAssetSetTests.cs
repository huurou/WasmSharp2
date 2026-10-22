using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal partial class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task 固定ケース集合を読む_六素材十五ケースの分類と採用値を保持する()
    {
        // Arrange
        var json = await ReadPreparationCasesAsync();
        var expected = new Dictionary<string, string[]>
        {
            ["invoke"] =
            [
                "invoke.empty",
                "invoke.single",
                "invoke.multi",
                "decode.small",
                "decode-validate.small",
            ],
            ["calls"] = ["loop.integer-direct", "loop.float-host", "loop.indirect"],
            ["memory"] = ["memory.copy-fill"],
            ["scan"] = ["scan.scalar", "scan.simd"],
            ["kernel"] = ["kernel.matrix"],
            ["preparation-large"] = ["decode.large", "decode-validate.large", "instantiate.large"],
        };

        // Act
        var definitions = JsonSerializer.Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definitions.Count).IsEqualTo(15);
            await Assert.That(definitions.Select(x => x.CaseId).Distinct().Count()).IsEqualTo(15);
            await Assert
                .That(definitions.Select(x => x.AssetId).Distinct())
                .IsEquivalentTo(expected.Keys);
            await Assert
                .That(definitions.Count(x => x.MeasurementStage == MeasurementStage.Invoke))
                .IsEqualTo(10);
            await Assert
                .That(definitions.Count(x => x.MeasurementStage == MeasurementStage.Decode))
                .IsEqualTo(2);
            await Assert
                .That(definitions.Count(x => x.MeasurementStage == MeasurementStage.DecodeValidate))
                .IsEqualTo(2);
            await Assert
                .That(definitions.Count(x => x.MeasurementStage == MeasurementStage.Instantiate))
                .IsEqualTo(1);
            foreach (var asset in expected)
            {
                var cases = definitions.Where(x => x.AssetId == asset.Key).ToArray();
                await Assert.That(cases.Select(x => x.CaseId)).IsEquivalentTo(asset.Value);
                await Assert.That(cases.Select(x => x.AssetSha256).Distinct().Count()).IsEqualTo(1);
            }
            foreach (var definition in definitions)
            {
                await Assert.That(definition.CaseRevision).IsEqualTo(1U);
                await Assert.That(definition.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
                await Assert
                    .That(definition.OperationDefinition.IncludesStateRestoration)
                    .IsFalse();
                await Assert
                    .That(definition.InputHash)
                    .IsEqualTo(
                        Convert.ToHexStringLower(
                            SHA256.HashData(
                                JsonSerializer.SerializeToUtf8Bytes(
                                    definition.Input,
                                    CaseJson.Options
                                )
                            )
                        )
                    );
            }
        }
    }

    [Test]
    public async Task 小さいmoduleの準備ケースを読む_新規moduleと独立した後検査期待値を保持する()
    {
        // Arrange
        var json = await ReadPreparationCasesAsync();

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x => x.CaseId is "decode.small" or "decode-validate.small")
            .ToArray();

        // Assert
        await Assert.That(definitions.Length).IsEqualTo(2);
        foreach (var definition in definitions)
        {
            using (Assert.Multiple())
            {
                await Assert.That(definition.AssetId).IsEqualTo("invoke");
                await Assert.That(definition.StatePolicy).IsEqualTo(StatePolicy.FreshModule);
                await Assert
                    .That(definition.MeasurementStage)
                    .IsEqualTo(
                        definition.CaseId == "decode.small"
                            ? MeasurementStage.Decode
                            : MeasurementStage.DecodeValidate
                    );
                await Assert
                    .That(definition.Workload)
                    .IsEquivalentTo(
                        new[]
                        {
                            new WorkloadQuantity("defined_functions", 3, WorkloadUnit.Functions),
                        }
                    );
                await Assert.That(definition.Input.FunctionExport).IsEqualTo("single");
                await Assert
                    .That(definition.Input.Arguments.Single())
                    .IsEqualTo(new NumericValue(NumericType.I32, "ffffffd6"));
                await Assert.That(definition.Input.Arrays.Count).IsEqualTo(0);
                await Assert
                    .That(definition.ExpectedResult.Values.Single())
                    .IsEqualTo(
                        new NumericValue(NumericType.I32, unchecked((uint)-42).ToString("x8"))
                    );
                await Assert
                    .That(
                        definition.ExpectedResult.Arrays.Count
                            + definition.ExpectedResult.Globals.Count
                            + definition.ExpectedResult.Tables.Count
                    )
                    .IsEqualTo(0);
            }
        }
    }
}
