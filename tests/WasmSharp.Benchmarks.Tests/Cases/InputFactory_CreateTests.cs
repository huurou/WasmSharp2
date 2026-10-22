using System.Buffers.Binary;
using System.Security.Cryptography;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class InputFactory_CreateTests
{
    [Test]
    public async Task 明示型の数値引数を復元する_整数とNaNと負のゼロのbit列を保つ()
    {
        // Arrange
        var original = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.multi");
        var definition = AssetLoadingFixture.WithInput(
            original,
            original.Input with
            {
                Arguments = [.. original.Input.Arguments, new(NumericType.F32, "7fc12345")],
            }
        );

        // Act
        var input = InputFactory.Create(definition);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(input.Arguments[0].AsI32()).IsEqualTo(unchecked((int)0x89abcdef));
            await Assert
                .That(input.Arguments[1].AsI64())
                .IsEqualTo(unchecked((long)0xfedcba9876543210));
            await Assert.That(input.Arguments[2].AsF64Bits()).IsEqualTo(0x8000000000000000UL);
            await Assert.That(input.Arguments[3].AsF32Bits()).IsEqualTo(0x7fc12345U);
        }
    }

    [Test]
    public async Task memoryとscanの固定入力を復元する_全領域の固定digestとlittleEndianが一致する()
    {
        // Arrange
        var definitions = AssetLoadingFixture.ReadCases();

        // Act & Assert
        foreach (
            var definition in definitions.Where(x =>
                x.CaseId is "memory.copy-fill" or "scan.scalar" or "scan.simd"
            )
        )
        {
            var input = InputFactory.Create(definition);
            using (Assert.Multiple())
            {
                await Assert
                    .That(input.Arrays.Length)
                    .IsEqualTo(definition.ExpectedResult.Arrays.Count);
                foreach (var array in input.Arrays)
                {
                    var expected = definition.ExpectedResult.Arrays.Single(x =>
                        x.Offset == array.Offset
                    );
                    await Assert.That(array.MemoryExport).IsEqualTo(expected.MemoryExport);
                    await Assert
                        .That(Convert.ToHexStringLower(SHA256.HashData(array.Bytes.AsSpan())))
                        .IsEqualTo(expected.Digest);
                }
            }
        }
    }

    [Test]
    public async Task 行列の固定seedを復元する_既知の先頭列と入力領域と再生成結果が一致する()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "kernel.matrix");

        // Act
        var input = InputFactory.Create(definition);
        var repeated = InputFactory.Create(definition);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(input.Arrays.Select(x => x.Offset))
                .IsEquivalentTo(new uint[] { 65536, 69632 });
            await Assert.That(input.Arrays.All(x => x.Bytes.Length == 4096)).IsTrue();
            await Assert
                .That(BinaryPrimitives.ReadUInt32LittleEndian(input.Arrays[0].Bytes.AsSpan()))
                .IsEqualTo(0x87985aa5U);
            await Assert
                .That(BinaryPrimitives.ReadUInt32LittleEndian(input.Arrays[0].Bytes.AsSpan()[4..]))
                .IsEqualTo(0x155b24a3U);
            await Assert
                .That(BinaryPrimitives.ReadUInt32LittleEndian(input.Arrays[1].Bytes.AsSpan()))
                .IsEqualTo(0x510c4619U);
            await Assert
                .That(
                    input
                        .Arrays.Zip(repeated.Arrays)
                        .All(x => x.First.Bytes.SequenceEqual(x.Second.Bytes))
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("0000000")]
    [Arguments("FFFFFFFF")]
    [Arguments("0000000g")]
    public async Task 数値bit列が固定形式に合わない_ケースと数値型を示して拒否する(string bits)
    {
        // Arrange
        var original = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.single");
        var definition = AssetLoadingFixture.WithInput(
            original,
            original.Input with
            {
                Arguments = [new(NumericType.I32, bits)],
            }
        );

        // Act & Assert
        var exception = await Assert
            .That(() => InputFactory.Create(definition))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("invoke.single").And.Contains("I32");
    }

    [Test]
    public async Task 配列生成方式に必要なseedがない_ケースと生成方式を示して拒否する()
    {
        // Arrange
        var original = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "kernel.matrix");
        var definition = AssetLoadingFixture.WithInput(
            original,
            original.Input with
            {
                Arrays = [original.Input.Arrays[0] with { Seed = null }],
            }
        );

        // Act & Assert
        var exception = await Assert
            .That(() => InputFactory.Create(definition))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("kernel.matrix").And.Contains("SeededU32");
    }
}
