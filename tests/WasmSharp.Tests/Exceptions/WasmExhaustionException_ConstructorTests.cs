using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

internal class WasmExhaustionException_ConstructorTests
{
    [Test]
    [Arguments(WasmProcessingStage.Instantiate, 1)]
    [Arguments(WasmProcessingStage.Invoke, 100)]
    public async Task 深さ上限の診断を指定する_原因と適用上限と発生位置を保持する(
        WasmProcessingStage stage,
        int limit
    )
    {
        // Arrange
        var location = new WasmFailureLocation(stage, 37, 2);
        var innerException = new InvalidOperationException("原因");

        // Act
        var exception = new WasmExhaustionException(
            "実行上限に到達した",
            WasmExhaustionReason.CallDepthLimit,
            limit,
            location,
            innerException
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(limit);
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Message).IsEqualTo("実行上限に到達した");
            await Assert.That(ReferenceEquals(exception.InnerException, innerException)).IsTrue();
            await Assert.That(exception.GetType().BaseType).IsEqualTo(typeof(WasmException));
        }
    }
}
