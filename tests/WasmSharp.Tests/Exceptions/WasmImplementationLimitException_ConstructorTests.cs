using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

internal class WasmImplementationLimitException_ConstructorTests
{
    [Test]
    [Arguments(WasmImplementationLimitReason.InputSize, WasmProcessingStage.Decode, int.MaxValue)]
    [Arguments(WasmImplementationLimitReason.CollectionSize, WasmProcessingStage.Invoke, 65536)]
    public async Task 保持上限の診断を指定する_原因と適用上限と発生位置を保持する(
        WasmImplementationLimitReason reason,
        WasmProcessingStage stage,
        int limit
    )
    {
        // Arrange
        var location = new WasmFailureLocation(stage, 0x100000000L, 3, 10);
        var innerException = new InvalidOperationException("原因");

        // Act
        var exception = new WasmImplementationLimitException(
            "保持上限に到達した",
            reason,
            limit,
            location,
            innerException
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Reason).IsEqualTo(reason);
            await Assert.That(exception.Limit).IsEqualTo(limit);
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Location?.ByteOffset).IsEqualTo(0x100000000L);
            await Assert.That(exception.Message).IsEqualTo("保持上限に到達した");
            await Assert.That(ReferenceEquals(exception.InnerException, innerException)).IsTrue();
            await Assert.That(exception.GetType().BaseType).IsEqualTo(typeof(WasmException));
        }
    }
}
