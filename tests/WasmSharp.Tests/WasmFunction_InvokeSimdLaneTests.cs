using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(
        0xDu,
        "i8x16.shuffle",
        "7B7B",
        (byte)0x7B,
        "000F101F070817181F100F0018170807",
        0x888708078F800F00UL,
        0x07088788000F808FUL
    )]
    [Arguments(
        0xEu,
        "i8x16.swizzle",
        "7B7B",
        (byte)0x7B,
        "",
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFu,
        "i8x16.splat",
        "7F",
        (byte)0x7B,
        "",
        0x8080808080808080UL,
        0x8080808080808080UL
    )]
    [Arguments(
        0x13u,
        "f32x4.splat",
        "7D",
        (byte)0x7B,
        "",
        0x7FC123457FC12345UL,
        0x7FC123457FC12345UL
    )]
    [Arguments(
        0x15u,
        "i8x16.extract_lane_s",
        "7B",
        (byte)0x7F,
        "0F",
        0x000000000000000FUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x1Au,
        "i16x8.replace_lane",
        "7B7F",
        (byte)0x7B,
        "07",
        0x0706050403020100UL,
        0x81800D0C0B0A0908UL
    )]
    [Arguments(
        0x1Du,
        "i64x2.extract_lane",
        "7B",
        (byte)0x7E,
        "01",
        0x0F0E0D0C0B0A0908UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x20u,
        "f32x4.replace_lane",
        "7B7D",
        (byte)0x7B,
        "03",
        0x0706050403020100UL,
        0x7FC123450B0A0908UL
    )]
    [Arguments(
        0x21u,
        "f64x2.extract_lane",
        "7B",
        (byte)0x7C,
        "01",
        0x0F0E0D0C0B0A0908UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x26u,
        "i8x16.lt_u",
        "7B7B",
        (byte)0x7B,
        "",
        0xFFFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x42u,
        "f32x4.ne",
        "7B7B",
        (byte)0x7B,
        "",
        0xFFFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x52u,
        "v128.bitselect",
        "7B7B7B",
        (byte)0x7B,
        "",
        0x0706050403020100UL,
        0x0F0E0D0C0B0A0908UL
    )]
    [Arguments(
        0x53u,
        "v128.any_true",
        "7B",
        (byte)0x7F,
        "",
        0x0000000000000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xC4u,
        "i64x2.bitmask",
        "7B",
        (byte)0x7F,
        "",
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    public async Task SIMD接続の各命令が関数にある_両入力の公開4段階で引数型と結果の全bitを保持する(
        uint opcode,
        string name,
        string parameters,
        byte resultType,
        string immediate,
        ulong expectedLow,
        ulong expectedHigh
    )
    {
        // Arrange
        var parameterTypes = Convert.FromHexString(parameters);
        var body = parameterTypes.SelectMany((_, x) => new byte[] { 0x20, (byte)x }).ToArray();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameterTypes, [resultType])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    [
                        .. body,
                        0xFD,
                        .. HostLinkingModuleBinary.Unsigned(opcode),
                        .. Convert.FromHexString(immediate),
                        0x0B,
                    ]
                )
            )
        );
        WasmValue[] vectors =
        [
            WasmValue.FromV128(0x0706050403020100UL, 0x0F0E0D0C0B0A0908UL),
            WasmValue.FromV128(0x8786858483828180UL, 0x8F8E8D8C8B8A8988UL),
            WasmValue.FromV128(0xAAAAAAAAAAAAAAAAUL, 0xAAAAAAAAAAAAAAAAUL),
        ];
        var operands = parameterTypes
            .Select(
                (x, i) =>
                    x switch
                    {
                        0x7B => vectors[i],
                        0x7F => WasmValue.FromI32(unchecked((int)0xFFFF8180)),
                        0x7E => WasmValue.FromI64(unchecked((long)0xFEDCBA9876543210)),
                        0x7D => WasmValue.FromF32Bits(0x7FC12345),
                        _ => WasmValue.FromF64Bits(0xFFF8123456789ABC),
                    }
            )
            .ToArray();
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        Array.Clear(bytes);
        foreach (var module in modules)
        {
            var actual = module
                .Validate()
                .Instantiate([])
                .GetFunction("run")
                .Invoke(operands)
                .Values;
            var bits = resultType switch
            {
                0x7B => actual[0].AsV128(),
                0x7F => ((ulong)unchecked((uint)actual[0].AsI32()), 0UL),
                0x7E => (unchecked((ulong)actual[0].AsI64()), 0UL),
                0x7D => ((ulong)actual[0].AsF32Bits(), 0UL),
                _ => (actual[0].AsF64Bits(), 0UL),
            };

            // Assert
            using (Assert.Multiple())
            {
                await Assert
                    .That(module.Functions[0].Instructions[parameterTypes.Length].Opcode.Code)
                    .IsEqualTo(opcode);
                await Assert.That(actual.Length).IsEqualTo(1);
                await Assert.That(bits).IsEqualTo((expectedLow, expectedHigh));
                await Assert
                    .That(
                        WasmSharp.Instructions.InstructionSet.TryGet(
                            new(0xFD, opcode),
                            out var descriptor
                        )
                    )
                    .IsTrue();
                await Assert.That(descriptor.Name).IsEqualTo(name);
            }
        }
    }
}
