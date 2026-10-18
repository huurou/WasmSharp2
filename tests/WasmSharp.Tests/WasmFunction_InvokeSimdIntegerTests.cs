using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(
        "6E",
        "7F7F7F7F7F7F7F7F7F7F7F7F7F7F7F7F",
        "01010101010101010101010101010101",
        "80808080808080808080808080808080"
    )]
    [Arguments(
        "66",
        "FFFF0000FF000001FF7F008001008000",
        "FFFF0000FF000001FF7F008001008000",
        "0000FFFFFF0001800000FFFFFF000180"
    )]
    [Arguments(
        "8201",
        "00800080008000800080008000800080",
        "00800080008000800080008000800080",
        "FF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7F"
    )]
    [Arguments("8001", "00800080FF7F0100FFFF00000200FEFF", "", "00800080FF7F01000100000002000200")]
    [Arguments(
        "808100",
        "00800080FF7F0100FFFF00000200FEFF",
        "",
        "00800080FF7F01000100000002000200"
    )]
    [Arguments(
        "8081808000",
        "00800080FF7F0100FFFF00000200FEFF",
        "",
        "00800080FF7F01000100000002000200"
    )]
    [Arguments("A801", "01000200030004000080FFFFFF7F0000", "", "0080FFFFFFFFFFFFFF7F000000000000")]
    [Arguments(
        "DF01",
        "0000000000000000FFFFFFFF00000080",
        "01000000020000000300000004000000",
        "FDFFFFFF020000000000000002000000"
    )]
    [Arguments("7C", "807F807F807F807F807F807F807F807F", "", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [Arguments(
        "BA01",
        "00800080008000800080008000800080",
        "00800080008000800080008000800080",
        "00000080000000800000008000000080"
    )]
    public async Task SIMD整数接続の境界値を実行する_両入力の公開4段階で固定幅の結果を返す(
        string opcode,
        string first,
        string second,
        string expected
    )
    {
        // Arrange
        byte[] parameters = second.Length == 0 ? [0x7B] : [0x7B, 0x7B];
        var pushes = parameters.SelectMany((_, x) => new byte[] { 0x20, (byte)x });
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameters, [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(
                ([], [.. pushes, 0xFD, .. Convert.FromHexString(opcode), 0x0B])
            )
        );
        WasmValue[] operands =
            second.Length == 0 ? [Vector(first)] : [Vector(first), Vector(second)];
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        foreach (var module in modules)
        {
            var actual = module
                .Validate()
                .Instantiate([])
                .GetFunction("run")
                .Invoke(operands)
                .Values;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(actual.Length).IsEqualTo(1);
                await Assert.That(actual[0].AsV128()).IsEqualTo(Vector(expected).AsV128());
            }
        }
    }

    [Test]
    [Arguments(0x6Bu, 8, 0x0202020202020202UL)]
    [Arguments(0x8Bu, 16, 0x0002000200020002UL)]
    [Arguments(0xABu, 32, 0x0000000200000002UL)]
    [Arguments(0xCBu, 64, 0x0000000000000002UL)]
    public async Task SIMD整数接続のshiftにV128とI32を順に渡す_全lane幅でシフト量を剰余化する(
        uint opcode,
        int width,
        ulong expected
    )
    {
        // Arrange
        var bytes = IntegerShiftModule(opcode, [0x7B, 0x7F]);
        var input = width switch
        {
            8 => 0x0101010101010101UL,
            16 => 0x0001000100010001UL,
            32 => 0x0000000100000001UL,
            _ => 1UL,
        };
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        foreach (var module in modules)
        {
            var actual = module
                .Validate()
                .Instantiate([])
                .GetFunction("run")
                .Invoke([WasmValue.FromV128(input, input), WasmValue.FromI32(width + 1)])
                .Values;

            // Assert
            await Assert.That(actual[0].AsV128()).IsEqualTo((expected, expected));
        }
    }

    private static byte[] IntegerShiftModule(uint opcode, byte[] parameters)
    {
        return HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameters, [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x20, 1, 0xFD, .. HostLinkingModuleBinary.Unsigned(opcode), 0x0B])
            )
        );
    }

    private static WasmValue Vector(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        return WasmValue.FromV128(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8))
        );
    }
}
