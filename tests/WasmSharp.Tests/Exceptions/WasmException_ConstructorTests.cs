using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

internal class WasmException_ConstructorTests
{
    [Test]
    public async Task 既存の構築経路を使う_診断なしでメッセージと元の例外を保持する()
    {
        // Arrange
        var innerException = new InvalidOperationException("原因");

        // Act
        WasmException[] withoutDiagnostics =
        [
            new WasmDecodeException(),
            new WasmDecodeException(null),
            new WasmDecodeException(null, null),
            new WasmValidateException(),
            new WasmValidateException(null),
            new WasmValidateException(null, null),
        ];
        WasmException[] withMessage =
        [
            new WasmDecodeException("失敗"),
            new WasmValidateException("失敗"),
        ];
        WasmException[] withInnerException =
        [
            new WasmDecodeException("失敗", innerException),
            new WasmValidateException("失敗", innerException),
        ];

        // Assert
        using (Assert.Multiple())
        {
            foreach (var exception in withoutDiagnostics)
            {
                await Assert.That(exception.Location).IsNull();
                await Assert.That(exception.InnerException).IsNull();
            }
            foreach (var exception in withMessage)
            {
                await Assert.That(exception.Location).IsNull();
                await Assert.That(exception.Message).IsEqualTo("失敗");
                await Assert.That(exception.InnerException).IsNull();
            }
            foreach (var exception in withInnerException)
            {
                await Assert.That(exception.Location).IsNull();
                await Assert.That(exception.Message).IsEqualTo("失敗");
                await Assert
                    .That(ReferenceEquals(exception.InnerException, innerException))
                    .IsTrue();
            }
        }
    }

    [Test]
    [Arguments(WasmProcessingStage.Decode)]
    [Arguments(WasmProcessingStage.Validate)]
    public async Task 診断を指定する_処理段階と入力位置と元の例外を保持する(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var location = new WasmFailureLocation(stage, 37, 2, 10);
        var innerException = new InvalidOperationException("原因");

        // Act
        WasmException exception =
            stage == WasmProcessingStage.Decode
                ? new WasmDecodeException("失敗", location, innerException)
                : new WasmValidateException("失敗", location, innerException);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Location?.Stage).IsEqualTo(stage);
            await Assert.That(exception.Location?.ByteOffset).IsEqualTo(37L);
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(2U);
            await Assert.That(exception.Location?.SectionId).IsEqualTo((byte)10);
            await Assert.That(exception.Message).IsEqualTo("失敗");
            await Assert.That(ReferenceEquals(exception.InnerException, innerException)).IsTrue();
        }
    }
}
