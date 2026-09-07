using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

internal class WasmFailureLocation_ConstructorTests
{
    [Test]
    public async Task 段階だけを指定する_不明な入力位置と関数とsectionをnullで保持する()
    {
        // Arrange
        var stage = WasmProcessingStage.Validate;

        // Act
        var location = new WasmFailureLocation(stage);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(location.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(location.ByteOffset).IsNull();
            await Assert.That(location.FunctionIndex).IsNull();
            await Assert.That(location.SectionId).IsNull();
        }
    }
}
