using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("410A41036A", (byte)0x7F, 13ul)]
    [Arguments("421342037D", (byte)0x7E, 16ul)]
    [Arguments("420050", (byte)0x7F, 1ul)]
    [Arguments("427F420154", (byte)0x7F, 0ul)]
    [Arguments("420079", (byte)0x7E, 64ul)]
    [Arguments("42007A", (byte)0x7E, 64ul)]
    [Arguments("427F7B", (byte)0x7E, 64ul)]
    [Arguments("430000C03F430000104092", (byte)0x7D, 0x40700000ul)]
    [Arguments("44000000000000F03F44000000000000004063", (byte)0x7F, 1ul)]
    [Arguments("428180808010A7", (byte)0x7F, 1ul)]
    [Arguments("44000000000000F83FB6", (byte)0x7D, 0x3FC00000ul)]
    [Arguments("434523C1FFBC", (byte)0x7F, 0xFFC12345ul)]
    [Arguments("41FF01C0", (byte)0x7F, 0xFFFFFFFFul)]
    public async Task 代表数値命令を公開4段階で呼び出す_生成dispatchから型と結果を返す(
        string instructions,
        byte resultKind,
        ulong expectedBits
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [resultKind])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var expected = resultKind switch
        {
            0x7F => WasmValue.FromI32(unchecked((int)expectedBits)),
            0x7E => WasmValue.FromI64(unchecked((long)expectedBits)),
            0x7D => WasmValue.FromF32Bits((uint)expectedBits),
            _ => throw new ArgumentOutOfRangeException(nameof(resultKind)),
        };

        // Act
        var result = WasmModule
            .Decode(bytes)
            .Validate()
            .Instantiate([])
            .GetFunction("run")
            .Invoke([]);

        // Assert
        await Assert.That(result.Values.Length).IsEqualTo(1);
        await Assert.That(result.Values[0]).IsEqualTo(expected);
    }

    [Test]
    [Arguments((byte)0, false)]
    [Arguments((byte)0, true)]
    [Arguments((byte)1, false)]
    [Arguments((byte)1, true)]
    [Arguments((byte)2, false)]
    [Arguments((byte)2, true)]
    [Arguments((byte)3, false)]
    [Arguments((byte)3, true)]
    [Arguments((byte)4, false)]
    [Arguments((byte)4, true)]
    [Arguments((byte)5, false)]
    [Arguments((byte)5, true)]
    [Arguments((byte)6, false)]
    [Arguments((byte)6, true)]
    [Arguments((byte)7, false)]
    [Arguments((byte)7, true)]
    public async Task 全飽和変換を最短と非最短FC番号で呼び出す_両Decode入口から目的整数型の切捨て結果を返す(
        byte code,
        bool useStream
    )
    {
        // Arrange
        var resultKind = code < 4 ? (byte)0x7F : (byte)0x7E;
        byte[] constant =
            code % 4 < 2 ? [0x43, 0, 0, 0xF0, 0x40] : [0x44, 0, 0, 0, 0, 0, 0, 0x1E, 0x40];
        var expected = code < 4 ? WasmValue.FromI32(7) : WasmValue.FromI64(7);
        foreach (var nonShortest in new[] { false, true })
        {
            byte[] number = nonShortest ? [(byte)(code | 0x80), 0] : [code];
            var bytes = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [resultKind])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], [.. constant, 0xFC, .. number, 0x0B]))
            );
            using var stream = new MemoryStream(bytes);

            // Act
            var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
            var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

            // Assert
            await Assert.That(result.Values.Length).IsEqualTo(1);
            await Assert.That(result.Values[0]).IsEqualTo(expected);
        }
    }
}
