using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryFormat_ReadInputTests
{
    [Test]
    [Arguments(5)]
    [Arguments(6)]
    public async Task 入力が保持上限以内_終端まで読み取ったバイト列を返す(int maxLength)
    {
        // Arrange
        byte[] bytes = [1, 2, 3, 4, 5];
        using var stream = new ChunkedReadStream(new MemoryStream(bytes), 3);

        // Act
        var result = ModuleBinaryFormat.ReadInput(stream, maxLength);

        // Assert
        await Assert.That(result.SequenceEqual(bytes)).IsTrue();
    }

    [Test]
    public async Task 追加後の入力が保持上限を超える_原因と上限と追加前の位置を通知する()
    {
        // Arrange
        using var stream = new ChunkedReadStream(new MemoryStream([1, 2, 3, 4, 5, 6]), 3);
        Action read = () => ModuleBinaryFormat.ReadInput(stream, 5);

        // Act & Assert
        var exception = await Assert.That(read).ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImplementationLimitReason.InputSize);
            await Assert.That(exception.Message).Contains("入力バイナリが保持上限を超え");
            await Assert.That(exception.Limit).IsEqualTo(5);
            await Assert.That(exception.Location).IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 3));
        }
    }
}
