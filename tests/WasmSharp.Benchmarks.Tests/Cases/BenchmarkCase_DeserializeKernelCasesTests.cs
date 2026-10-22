using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal partial class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task 行列積の固定ケースを読む_入力と出力の分離と公開操作数を保持する()
    {
        // Arrange
        var json = await ReadKernelCasesAsync();
        ArrayInput[] inputs =
        [
            new(
                "memory",
                65536,
                ArrayElementType.U32,
                1024,
                ArrayInputPattern.SeededU32,
                0x12345678,
                null
            ),
            new(
                "memory",
                69632,
                ArrayElementType.U32,
                1024,
                ArrayInputPattern.SeededU32,
                0x9e3779b9,
                null
            ),
        ];
        NumericValue[] arguments =
        [
            new(NumericType.I32, "00010000"),
            new(NumericType.I32, "00011000"),
            new(NumericType.I32, "00012000"),
        ];

        // Act
        var definition = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Single(x => x.CaseId == "kernel.matrix");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definition.AssetId).IsEqualTo("kernel");
            await Assert.That(definition.CaseRevision).IsEqualTo(1U);
            await Assert.That(definition.MeasurementStage).IsEqualTo(MeasurementStage.Invoke);
            await Assert.That(definition.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
            await Assert.That(definition.OperationDefinition.IncludesStateRestoration).IsFalse();
            await Assert.That(definition.StatePolicy).IsEqualTo(StatePolicy.OverwriteOutput);
            await Assert.That(definition.Input.FunctionExport).IsEqualTo("matrix");
            await Assert.That(definition.Input.Arguments.SequenceEqual(arguments)).IsTrue();
            await Assert.That(definition.Input.Arrays.SequenceEqual(inputs)).IsTrue();
            await Assert
                .That(definition.Workload)
                .IsEquivalentTo(
                    new[]
                    {
                        new WorkloadQuantity("matrix_output", 1024, WorkloadUnit.Elements),
                        new WorkloadQuantity("multiply_accumulate", 32768, WorkloadUnit.Iterations),
                    }
                );
            await Assert
                .That(definition.InputHash)
                .IsEqualTo(
                    Convert.ToHexStringLower(
                        SHA256.HashData(
                            JsonSerializer.SerializeToUtf8Bytes(definition.Input, CaseJson.Options)
                        )
                    )
                );
            await Assert.That(definition.ExpectedResult.Values.Count).IsEqualTo(0);
            await Assert.That(definition.ExpectedResult.Globals.Count).IsEqualTo(0);
            await Assert.That(definition.ExpectedResult.Tables.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 行列積の固定期待値を読む_多倍長整数の独立計算による全出力digestと一致する()
    {
        // Arrange
        var json = await ReadKernelCasesAsync();
        var left = CreateKernelInput(0x12345678);
        var right = CreateKernelInput(0x9e3779b9);
        var output = new byte[1024 * sizeof(uint)];
        var wrappedElements = 0;
        for (var index = 0; index < 1024; index++)
        {
            var row = index / 32;
            var column = index % 32;
            // 積和は桁あふれしない整数で計算し、最後にだけmod2^32を取る。
            var exact = Enumerable
                .Range(0, 32)
                .Aggregate(
                    BigInteger.Zero,
                    (sum, x) => sum + (BigInteger)left[row * 32 + x] * right[x * 32 + column]
                );
            if (exact > uint.MaxValue)
            {
                wrappedElements++;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(
                output.AsSpan(index * sizeof(uint)),
                (uint)(exact & uint.MaxValue)
            );
        }
        var digest = Convert.ToHexStringLower(SHA256.HashData(output));

        // Act
        var definition = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Single(x => x.CaseId == "kernel.matrix");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(wrappedElements).IsEqualTo(1024);
            await Assert
                .That(definition.ExpectedResult.Arrays.Single())
                .IsEqualTo(
                    new ArrayDigest("memory", 73728, ArrayElementType.U32, 1024, "sha256", digest)
                );
        }
    }

    private static uint[] CreateKernelInput(uint seed)
    {
        var values = new uint[1024];
        var state = seed;
        for (var index = 0; index < values.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            values[index] = state;
        }
        return values;
    }

    private static Task<string> ReadKernelCasesAsync()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
        );
    }
}
