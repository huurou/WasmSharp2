using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryReader_EnsureCollectionSizeTests
{
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    public async Task 要素数が保持上限以内_読み取り位置を変更せず受け付ける(int difference)
    {
        // Arrange
        var count = (ulong)((long)Array.MaxLength + difference);

        // Act
        var position = CheckSize(count);

        // Assert
        await Assert.That(position).IsEqualTo(19L);
    }

    [Test]
    public async Task 要素数が保持上限を超える_原因と上限と宣言位置を通知する()
    {
        // Arrange
        var count = (ulong)Array.MaxLength + 1;

        // Act & Assert
        var exception = await Assert.That(() => CheckSize(count)).ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert.That(exception.Location).IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 17, 3, 10));
        }
    }

    private static long CheckSize(ulong count)
    {
        var reader = new ModuleBinaryReader([], 19, 10, 3);
        reader.EnsureCollectionSize(count, 17, "要素数の保持上限を超えました。");
        return reader.Position;
    }
}
