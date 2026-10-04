using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryFormat_RequireEndTests
{
    [Test]
    public async Task 診断用モードで宣言終端に一致する_物理残量があっても成功する()
    {
        // Arrange
        var parent = new ModuleBinaryReader([0, 0], mode: ModuleReadMode.Diagnostic);
        var child = parent.ReadRange(1);
        child.ReadByte();

        // Act
        ModuleBinaryFormat.RequireEnd(ref child);
        var position = child.Position;
        var remaining = child.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(position).IsEqualTo(1L);
            await Assert.That(remaining).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(2u, 1, 101L)]
    [Arguments(1u, 2, 102L)]
    [Arguments(uint.MaxValue, 2, 102L)]
    public async Task 診断用モードで宣言終端と不一致_過不足ともDecode失敗になる(
        uint length,
        int readCount,
        long offset
    )
    {
        // Arrange
        byte[] bytes = [0, 0];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var parent = new ModuleBinaryReader(bytes, 100, 10, 3, ModuleReadMode.Diagnostic);
                var child = parent.ReadRange(length);
                for (var index = 0; index < readCount; index++)
                {
                    child.ReadByte();
                }
                ModuleBinaryFormat.RequireEnd(ref child);
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith("section size mismatch", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 3, 10));
        }
    }
}
