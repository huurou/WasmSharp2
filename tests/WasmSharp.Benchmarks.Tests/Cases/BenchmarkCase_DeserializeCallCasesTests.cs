using System.Globalization;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal partial class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task 呼び出し素材の固定ケースを読む_公開操作数とguest仕事量を分けて保持する()
    {
        // Arrange
        var json = await ReadCallCasesAsync();

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x =>
                x.MeasurementStage == MeasurementStage.Invoke && x.AssetId is "invoke" or "calls"
            )
            .ToArray();

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(definitions.Select(x => x.CaseId))
                .IsEquivalentTo(
                    new[]
                    {
                        "invoke.empty",
                        "invoke.single",
                        "invoke.multi",
                        "loop.integer-direct",
                        "loop.float-host",
                        "loop.indirect",
                    }
                );
            foreach (var definition in definitions)
            {
                await Assert.That(definition.CaseRevision).IsEqualTo(1U);
                await Assert.That(definition.MeasurementStage).IsEqualTo(MeasurementStage.Invoke);
                await Assert.That(definition.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
                await Assert
                    .That(definition.OperationDefinition.IncludesStateRestoration)
                    .IsFalse();
                await Assert.That(definition.StatePolicy).IsEqualTo(StatePolicy.ReadOnly);
                await Assert.That(definition.Input.Arrays.Count).IsEqualTo(0);
                await Assert.That(definition.ExpectedResult.Arrays.Count).IsEqualTo(0);
                await Assert.That(definition.ExpectedResult.Globals.Count).IsEqualTo(0);
                if (definition.CaseId.StartsWith("loop.", StringComparison.Ordinal))
                {
                    await Assert.That(definition.AssetId).IsEqualTo("calls");
                    await Assert
                        .That(definition.Workload.Single())
                        .IsEqualTo(
                            new WorkloadQuantity("guest_loop", 4096, WorkloadUnit.Iterations)
                        );
                }
                else
                {
                    await Assert.That(definition.AssetId).IsEqualTo("invoke");
                    await Assert.That(definition.Workload.Count).IsEqualTo(0);
                }
            }
        }
    }

    [Test]
    public async Task 呼び出しケースの期待値を読む_手計算と独立した閉形式のbit列に一致する()
    {
        // Arrange
        var json = await ReadCallCasesAsync();
        // 偶奇の一組で-1、間接callの一組で+4、半分の総和はn(n-1)/4
        const uint ITERATIONS = 4096;
        var integer = unchecked((uint)(17 - (int)ITERATIONS / 2));
        var floating = 1.5 + (double)ITERATIONS * (ITERATIONS - 1) / 4;
        var indirect = 17 + ITERATIONS / 2 * 4;

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .ToDictionary(x => x.CaseId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definitions["invoke.empty"].ExpectedResult.Values.Count).IsEqualTo(0);
            await Assert
                .That(definitions["invoke.single"].Input.Arguments.Single())
                .IsEqualTo(new NumericValue(NumericType.I32, "ffffffd6"));
            await Assert
                .That(definitions["invoke.single"].ExpectedResult.Values.Single())
                .IsEqualTo(new NumericValue(NumericType.I32, "ffffffd6"));
            NumericValue[] multi =
            [
                new(NumericType.I32, "89abcdef"),
                new(NumericType.I64, "fedcba9876543210"),
                new(NumericType.F64, "8000000000000000"),
            ];
            await Assert
                .That(definitions["invoke.multi"].Input.Arguments.SequenceEqual(multi))
                .IsTrue();
            await Assert
                .That(definitions["invoke.multi"].ExpectedResult.Values.SequenceEqual(multi))
                .IsTrue();
            await Assert
                .That(definitions["loop.integer-direct"].Input.Arguments.Single())
                .IsEqualTo(new NumericValue(NumericType.I32, "00000011"));
            await Assert
                .That(definitions["loop.float-host"].Input.Arguments.Single())
                .IsEqualTo(new NumericValue(NumericType.F64, "3ff8000000000000"));
            await Assert
                .That(definitions["loop.indirect"].Input.Arguments.Single())
                .IsEqualTo(new NumericValue(NumericType.I32, "00000011"));
            await Assert
                .That(definitions["loop.integer-direct"].ExpectedResult.Values.Single())
                .IsEqualTo(
                    new NumericValue(
                        NumericType.I32,
                        integer.ToString("x8", CultureInfo.InvariantCulture)
                    )
                );
            await Assert
                .That(definitions["loop.float-host"].ExpectedResult.Values.Single())
                .IsEqualTo(
                    new NumericValue(
                        NumericType.F64,
                        BitConverter
                            .DoubleToUInt64Bits(floating)
                            .ToString("x16", CultureInfo.InvariantCulture)
                    )
                );
            await Assert
                .That(definitions["loop.indirect"].ExpectedResult.Values.Single())
                .IsEqualTo(
                    new NumericValue(
                        NumericType.I32,
                        indirect.ToString("x8", CultureInfo.InvariantCulture)
                    )
                );
        }
    }

    [Test]
    public async Task 間接呼び出しの固定ケースを読む_tableの要素数と関数の順序を保持する()
    {
        // Arrange
        var json = await ReadCallCasesAsync();

        // Act
        var definition = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Single(x => x.CaseId == "loop.indirect");

        // Assert
        var table = definition.ExpectedResult.Tables.Single();
        using (Assert.Multiple())
        {
            await Assert.That(table.ExportName).IsEqualTo("functions");
            await Assert.That(table.Length).IsEqualTo(2U);
            await Assert
                .That(
                    table.Elements.SequenceEqual(
                        new TableElementExpectation[] { new(0, "add_one"), new(1, "add_three") }
                    )
                )
                .IsTrue();
        }
    }

    private static Task<string> ReadCallCasesAsync()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
        );
    }
}
