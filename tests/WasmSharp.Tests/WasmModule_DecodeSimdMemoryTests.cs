using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("FD00")]
    [Arguments("FD0080")]
    [Arguments("FD0000")]
    [Arguments("FD000080")]
    [Arguments("FD54")]
    [Arguments("FD5480")]
    [Arguments("FD5400")]
    [Arguments("FD540080")]
    [Arguments("FD540000")]
    public async Task SIMDメモリ接続の即値が切断される_両入力でDecodeの境界診断と元位置を返す(
        string hex
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7B, Convert.FromHexString(hex));
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new ChunkedReadStream(new MemoryStream(bytes));

            // Act & Assert
            var exception = await Assert
                .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                .ThrowsExactly<WasmDecodeException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(
                        exception!.Message.StartsWith(
                            "unexpected end of section or function",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length, 0, 10));
                await Assert.That(stream.CanRead).IsTrue();
            }
        }
    }

    [Test]
    [Arguments((byte)0x00, (byte)0)]
    [Arguments((byte)0x54, (byte)15)]
    [Arguments((byte)0x55, (byte)7)]
    [Arguments((byte)0x56, (byte)3)]
    [Arguments((byte)0x57, (byte)1)]
    [Arguments((byte)0x5C, (byte)0)]
    public async Task SIMDメモリ接続の完全な即値を読む_両入力で非最短LEBとoffsetとlaneを欠落なく保持する(
        byte opcode,
        byte lane
    )
    {
        // Arrange
        var hasLane = opcode is >= 0x54 and <= 0x5B;
        byte[] laneBytes = hasLane ? [lane] : [];
        var bytes = ConstantModuleBinary.Create(
            0x7B,
            [0xFD, opcode, 0x80, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, .. laneBytes, 0x0B]
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];

        // Assert
        foreach (var module in modules)
        {
            var actual = module.Functions[0].Instructions[0];
            using (Assert.Multiple())
            {
                await Assert.That(actual.MemoryArgument.Alignment).IsEqualTo(0u);
                await Assert.That(actual.MemoryArgument.Offset).IsEqualTo(uint.MaxValue);
                await Assert.That(actual.LaneIndex).IsEqualTo(lane);
                await Assert.That(actual.ByteOffset).IsEqualTo(33L);
                await Assert.That(module.FunctionCodes).IsEmpty();
            }
        }
    }
}
