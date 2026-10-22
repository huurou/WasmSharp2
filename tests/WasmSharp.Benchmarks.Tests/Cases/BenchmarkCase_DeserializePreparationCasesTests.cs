using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal partial class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task 大きいmoduleの固定ケースを読む_三つの準備段階と新規生成の条件を保持する()
    {
        // Arrange
        var json = await ReadPreparationCasesAsync();
        var stages = new Dictionary<string, MeasurementStage>
        {
            ["decode.large"] = MeasurementStage.Decode,
            ["decode-validate.large"] = MeasurementStage.DecodeValidate,
            ["instantiate.large"] = MeasurementStage.Instantiate,
        };

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x => x.AssetId == "preparation-large")
            .ToArray();

        // Assert
        await Assert.That(definitions.Select(x => x.CaseId)).IsEquivalentTo(stages.Keys);
        foreach (var definition in definitions)
        {
            using (Assert.Multiple())
            {
                await Assert.That(definition.CaseRevision).IsEqualTo(1U);
                await Assert.That(definition.MeasurementStage).IsEqualTo(stages[definition.CaseId]);
                await Assert.That(definition.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
                await Assert
                    .That(definition.OperationDefinition.IncludesStateRestoration)
                    .IsFalse();
                await Assert
                    .That(definition.StatePolicy)
                    .IsEqualTo(
                        definition.MeasurementStage == MeasurementStage.Instantiate
                            ? StatePolicy.FreshInstance
                            : StatePolicy.FreshModule
                    );
                await Assert
                    .That(definition.Workload)
                    .IsEquivalentTo(
                        new[]
                        {
                            new WorkloadQuantity("defined_functions", 256, WorkloadUnit.Functions),
                            new WorkloadQuantity("data", 65536, WorkloadUnit.Bytes),
                        }
                    );
                await Assert.That(definition.Input.FunctionExport).IsEqualTo("f254");
                await Assert
                    .That(definition.Input.Arguments.Single())
                    .IsEqualTo(new NumericValue(NumericType.I32, "00000011"));
                await Assert.That(definition.Input.Arrays.Count).IsEqualTo(0);
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
        await Assert.That(definitions.Select(x => x.AssetSha256).Distinct().Count()).IsEqualTo(1);
        await Assert.That(definitions.Select(x => x.InputHash).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task 大きいmoduleの固定期待値を読む_独立したdata計算とstartとelementの状態を保持する()
    {
        // Arrange
        var json = await ReadPreparationCasesAsync();
        var data = Enumerable.Range(0, 65536).Select(x => (byte)(x % 256)).ToArray();
        var digest = Convert.ToHexStringLower(SHA256.HashData(data));

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x => x.AssetId == "preparation-large")
            .ToArray();

        // Assert
        await Assert.That(definitions.Length).IsEqualTo(3);
        foreach (var definition in definitions)
        {
            using (Assert.Multiple())
            {
                await Assert
                    .That(definition.ExpectedResult.Values.Single())
                    .IsEqualTo(new NumericValue(NumericType.I32, (17 + 254).ToString("x8")));
                await Assert
                    .That(definition.ExpectedResult.Arrays.Single())
                    .IsEqualTo(
                        new ArrayDigest("memory", 0, ArrayElementType.U8, 65536, "sha256", digest)
                    );
                await Assert
                    .That(definition.ExpectedResult.Globals)
                    .IsEquivalentTo(
                        new[]
                        {
                            new GlobalExpectation(
                                "seed",
                                new NumericValue(NumericType.I32, 41.ToString("x8"))
                            ),
                            new GlobalExpectation(
                                "start_result",
                                new NumericValue(NumericType.I32, (41 + 1).ToString("x8"))
                            ),
                        }
                    );
                var table = definition.ExpectedResult.Tables.Single();
                await Assert.That(table.ExportName).IsEqualTo("functions");
                await Assert.That(table.Length).IsEqualTo(4U);
                await Assert
                    .That(table.Elements)
                    .IsEquivalentTo(
                        new[]
                        {
                            new TableElementExpectation(0, "f000"),
                            new TableElementExpectation(1, "f127"),
                            new TableElementExpectation(2, "f254"),
                            new TableElementExpectation(3, null),
                        }
                    );
            }
        }
    }

    private static Task<string> ReadPreparationCasesAsync()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
        );
    }
}
