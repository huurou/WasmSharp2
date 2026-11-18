using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryFormat_EnsureInputSizeTests
{
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    public async Task 追加後の入力が保持上限以内_受け付ける(int difference)
    {
        // Arrange
        var bufferedLength = (long)Array.MaxLength - 4096 + difference;

        // Act & Assert
        await Assert.That(() => ModuleBinaryFormat.EnsureInputSize(bufferedLength, 4096)).ThrowsNothing();
    }

    [Test]
    public async Task 追加後の入力が保持上限を超える_原因と上限と追加前の位置を通知する()
    {
        // Arrange
        var bufferedLength = (long)Array.MaxLength - 2;

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleBinaryFormat.EnsureInputSize(bufferedLength, 3))
            .ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImplementationLimitReason.InputSize);
            await Assert.That(exception.Message).Contains("入力バイナリが保持上限を超え");
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, bufferedLength));
        }
    }
}
