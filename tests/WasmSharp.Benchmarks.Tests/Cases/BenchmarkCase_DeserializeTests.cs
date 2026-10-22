using System.Text.Json;
using System.Text.Json.Nodes;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task 小さいケースfixtureを読む_操作と入力と固定期待値を復元する()
    {
        // Arrange
        var json = await ReadFixtureAsync();

        // Act
        var definition = JsonSerializer.Deserialize<BenchmarkCase>(json, CaseJson.Options)!;
        var restored = JsonSerializer.Deserialize<BenchmarkCase>(
            JsonSerializer.Serialize(definition, CaseJson.Options),
            CaseJson.Options
        )!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(restored.CaseId).IsEqualTo("fixture.invoke");
            await Assert.That(restored.CaseRevision).IsEqualTo(2U);
            await Assert.That(restored.AssetId).IsEqualTo("fixture");
            await Assert.That(restored.AssetSha256).IsEqualTo(new string('a', 64));
            await Assert.That(restored.InputHash).IsEqualTo(new string('b', 64));
            await Assert.That(restored.MeasurementStage).IsEqualTo(MeasurementStage.Invoke);
            await Assert.That(restored.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
            await Assert
                .That(restored.OperationDefinition.Description)
                .IsEqualTo("公開Invokeを1回");
            await Assert.That(restored.Workload.Single().Count).IsEqualTo(4UL);
            await Assert.That(restored.Workload.Single().Unit).IsEqualTo(WorkloadUnit.Elements);
            await Assert.That(restored.StatePolicy).IsEqualTo(StatePolicy.OverwriteOutput);
            await Assert.That(restored.OperationDefinition.IncludesStateRestoration).IsFalse();
            await Assert.That(restored.ComparisonGroup).IsEqualTo("fixture.same-input");
            await Assert.That(restored.Input.FunctionExport).IsEqualTo("run");
            await Assert
                .That(restored.Input.Arguments[0])
                .IsEqualTo(new NumericValue(NumericType.I32, "ffffffff"));
            await Assert
                .That(restored.Input.Arguments[1])
                .IsEqualTo(new NumericValue(NumericType.F64, "8000000000000000"));
            var input = restored.Input.Arrays.Single();
            await Assert.That(input.MemoryExport).IsEqualTo("memory");
            await Assert.That(input.Offset).IsEqualTo(16U);
            await Assert.That(input.Length).IsEqualTo(4U);
            await Assert.That(input.ElementType).IsEqualTo(ArrayElementType.U32);
            await Assert.That(input.Pattern).IsEqualTo(ArrayInputPattern.SeededU32);
            await Assert.That(input.Seed).IsEqualTo(uint.MaxValue);
            await Assert.That(input.FillByte).IsNull();
            await Assert
                .That(
                    restored.ExpectedResult.Values.SequenceEqual(
                        new NumericValue[]
                        {
                            new(NumericType.I32, "ffffffff"),
                            new(NumericType.I64, "ffffffffffffffff"),
                            new(NumericType.F32, "80000000"),
                            new(NumericType.F64, "7ff8000000000042"),
                        }
                    )
                )
                .IsTrue();
            var output = restored.ExpectedResult.Arrays.Single();
            await Assert.That(output.MemoryExport).IsEqualTo("memory");
            await Assert.That(output.Offset).IsEqualTo(64U);
            await Assert.That(output.ElementType).IsEqualTo(ArrayElementType.U32);
            await Assert.That(output.Length).IsEqualTo(4U);
            await Assert.That(output.HashAlgorithm).IsEqualTo("SHA-256");
            await Assert.That(output.Digest).IsEqualTo(new string('c', 64));
            await Assert
                .That(restored.ExpectedResult.Globals.Single())
                .IsEqualTo(new GlobalExpectation("started", new(NumericType.I32, "00000001")));
            await Assert.That(restored.ExpectedResult.Tables.Single().Length).IsEqualTo(1U);
            await Assert
                .That(restored.ExpectedResult.Tables.Single().Elements.Single())
                .IsEqualTo(new TableElementExpectation(0U, "run"));
        }
    }

    [Test]
    [Arguments("type")]
    [Arguments("bits")]
    public async Task 数値期待値の必須項目が欠ける_既定値へ補完せず拒否する(string property)
    {
        // Arrange
        var fixture = JsonNode.Parse(await ReadFixtureAsync())!;
        fixture["expected_result"]!["values"]![0]!.AsObject().Remove(property);

        // Act & Assert
        await Assert
            .That(() =>
                JsonSerializer.Deserialize<BenchmarkCase>(fixture.ToJsonString(), CaseJson.Options)
            )
            .ThrowsExactly<JsonException>();
    }

    [Test]
    [Arguments("length")]
    [Arguments("hash_algorithm")]
    [Arguments("digest")]
    public async Task 配列期待値の必須項目が欠ける_既定値へ補完せず拒否する(string property)
    {
        // Arrange
        var fixture = JsonNode.Parse(await ReadFixtureAsync())!;
        fixture["expected_result"]!["arrays"]![0]!.AsObject().Remove(property);

        // Act & Assert
        await Assert
            .That(() =>
                JsonSerializer.Deserialize<BenchmarkCase>(fixture.ToJsonString(), CaseJson.Options)
            )
            .ThrowsExactly<JsonException>();
    }

    [Test]
    public async Task bit列がnullである_値のない期待値を拒否する()
    {
        // Arrange
        var fixture = JsonNode.Parse(await ReadFixtureAsync())!;
        fixture["expected_result"]!["values"]![0]!["bits"] = null;

        // Act & Assert
        await Assert
            .That(() =>
                JsonSerializer.Deserialize<BenchmarkCase>(fixture.ToJsonString(), CaseJson.Options)
            )
            .ThrowsExactly<JsonException>();
    }

    [Test]
    [Arguments("decode", MeasurementStage.Decode, "fresh_module", StatePolicy.FreshModule)]
    [Arguments(
        "decode_validate",
        MeasurementStage.DecodeValidate,
        "fresh_module",
        StatePolicy.FreshModule
    )]
    [Arguments(
        "instantiate",
        MeasurementStage.Instantiate,
        "fresh_instance",
        StatePolicy.FreshInstance
    )]
    [Arguments("invoke", MeasurementStage.Invoke, "read_only", StatePolicy.ReadOnly)]
    [Arguments("invoke", MeasurementStage.Invoke, "fixed_point", StatePolicy.FixedPoint)]
    public async Task 区間と開始状態を保存する_独立した分類として復元する(
        string stage,
        MeasurementStage expectedStage,
        string state,
        StatePolicy expectedState
    )
    {
        // Arrange
        var fixture = JsonNode.Parse(await ReadFixtureAsync())!;
        fixture["measurement_stage"] = stage;
        fixture["state_policy"] = state;

        // Act
        var definition = JsonSerializer.Deserialize<BenchmarkCase>(
            fixture.ToJsonString(),
            CaseJson.Options
        )!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definition.MeasurementStage).IsEqualTo(expectedStage);
            await Assert.That(definition.StatePolicy).IsEqualTo(expectedState);
        }
    }

    private static Task<string> ReadFixtureAsync()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "benchmark-case.json")
        );
    }
}
