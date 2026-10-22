using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal partial class BenchmarkCase_DeserializeTests
{
    [Test]
    public async Task メモリ素材の固定ケースを読む_公開操作数と入力hashを保持する()
    {
        // Arrange
        var json = await ReadMemoryCasesAsync();

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x => x.AssetId is "memory" or "scan")
            .ToArray();

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(definitions.Select(x => x.CaseId))
                .IsEquivalentTo(new[] { "memory.copy-fill", "scan.scalar", "scan.simd" });
            foreach (var definition in definitions)
            {
                await Assert.That(definition.CaseRevision).IsEqualTo(1U);
                await Assert.That(definition.MeasurementStage).IsEqualTo(MeasurementStage.Invoke);
                await Assert.That(definition.OperationDefinition.OperationsPerInvoke).IsEqualTo(1U);
                await Assert
                    .That(definition.OperationDefinition.IncludesStateRestoration)
                    .IsFalse();
                await Assert.That(definition.Input.Arguments.Count).IsEqualTo(0);
                await Assert.That(definition.ExpectedResult.Globals.Count).IsEqualTo(0);
                await Assert.That(definition.ExpectedResult.Tables.Count).IsEqualTo(0);
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
    public async Task コピーと充填の固定ケースを読む_独立した各領域を最終状態へ揃える()
    {
        // Arrange
        var json = await ReadMemoryCasesAsync();
        var copiedBytes = Enumerable.Range(0, 65536).Select(x => (byte)(x % 256)).ToArray();
        var filledBytes = Enumerable.Repeat((byte)0xa5, 65536).ToArray();
        var copiedDigest = Convert.ToHexStringLower(SHA256.HashData(copiedBytes));
        var filledDigest = Convert.ToHexStringLower(SHA256.HashData(filledBytes));
        ArrayInput[] initial =
        [
            new(
                "memory",
                0,
                ArrayElementType.U8,
                65536,
                ArrayInputPattern.IndexModulo256,
                null,
                null
            ),
            new(
                "memory",
                65536,
                ArrayElementType.U8,
                65536,
                ArrayInputPattern.RepeatedByte,
                null,
                0xa5
            ),
            new(
                "memory",
                131072,
                ArrayElementType.U8,
                65536,
                ArrayInputPattern.IndexModulo256,
                null,
                null
            ),
        ];
        ArrayDigest[] final =
        [
            new("memory", 0, ArrayElementType.U8, 65536, "sha256", copiedDigest),
            new("memory", 65536, ArrayElementType.U8, 65536, "sha256", filledDigest),
            new("memory", 131072, ArrayElementType.U8, 65536, "sha256", copiedDigest),
        ];

        // Act
        var definition = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Single(x => x.CaseId == "memory.copy-fill");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definition.AssetId).IsEqualTo("memory");
            await Assert.That(definition.Input.FunctionExport).IsEqualTo("copy_fill");
            await Assert.That(definition.StatePolicy).IsEqualTo(StatePolicy.FixedPoint);
            await Assert.That(definition.Input.Arrays.SequenceEqual(initial)).IsTrue();
            await Assert.That(definition.ExpectedResult.Arrays.SequenceEqual(final)).IsTrue();
            await Assert
                .That(definition.Workload)
                .IsEquivalentTo(
                    new[]
                    {
                        new WorkloadQuantity("fill", 65536, WorkloadUnit.Bytes),
                        new WorkloadQuantity("copy", 65536, WorkloadUnit.Bytes),
                    }
                );
            // 各出力の末尾1バイトを加算するため、165+255=420
            await Assert
                .That(definition.ExpectedResult.Values.Single())
                .IsEqualTo(new NumericValue(NumericType.I32, "000001a4"));
        }
    }

    [Test]
    public async Task スカラーとSIMDの固定ケースを読む_同じ入力仕事量と独立計算の期待値を保持する()
    {
        // Arrange
        var json = await ReadMemoryCasesAsync();
        const uint ELEMENTS = 16384;
        var bytes = new byte[ELEMENTS * sizeof(uint)];
        for (uint index = 0; index < ELEMENTS; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan((int)index * sizeof(uint)),
                index % 256
            );
        }
        // 0〜255の総和が64組で2,088,960になる。
        var sum = ELEMENTS / 256 * (255 * 256 / 2);
        var expected = new NumericValue(
            NumericType.I32,
            sum.ToString("x8", CultureInfo.InvariantCulture)
        );
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));

        // Act
        var definitions = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(json, CaseJson.Options)!
            .Where(x => x.CaseId is "scan.scalar" or "scan.simd")
            .OrderBy(x => x.CaseId)
            .ToArray();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(definitions.Length).IsEqualTo(2);
            await Assert.That(sum).IsEqualTo(2088960U);
            await Assert
                .That(definitions.Select(x => x.Input.FunctionExport))
                .IsEquivalentTo(new[] { "scalar", "simd" });
            await Assert
                .That(definitions.Select(x => x.AssetSha256).Distinct().Count())
                .IsEqualTo(1);
            foreach (var definition in definitions)
            {
                await Assert.That(definition.AssetId).IsEqualTo("scan");
                await Assert.That(definition.ComparisonGroup).IsEqualTo("scan.sum-u32");
                await Assert.That(definition.StatePolicy).IsEqualTo(StatePolicy.ReadOnly);
                await Assert
                    .That(definition.Workload.Single())
                    .IsEqualTo(new WorkloadQuantity("scan", ELEMENTS, WorkloadUnit.Elements));
                await Assert
                    .That(definition.Input.Arrays.Single())
                    .IsEqualTo(
                        new ArrayInput(
                            "memory",
                            0,
                            ArrayElementType.U32,
                            ELEMENTS,
                            ArrayInputPattern.IndexModulo256,
                            null,
                            null
                        )
                    );
                await Assert.That(definition.ExpectedResult.Values.Single()).IsEqualTo(expected);
                await Assert
                    .That(definition.ExpectedResult.Arrays.Single())
                    .IsEqualTo(
                        new ArrayDigest(
                            "memory",
                            0,
                            ArrayElementType.U32,
                            ELEMENTS,
                            "sha256",
                            digest
                        )
                    );
            }
        }
    }

    private static Task<string> ReadMemoryCasesAsync()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
        );
    }
}
