using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules.Definitions;

internal class ElementDefinition_PassiveTests
{
    [Test]
    public async Task 空の初期化式列を指定する_対象を持たず型と位置と非defaultの空配列を保持する()
    {
        // Act
        var segment = ElementDefinition.Passive(WasmValueKind.ExternRef, [], 32);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segment.Mode).IsEqualTo(ElementMode.Passive);
            await Assert.That(segment.ActiveTarget).IsNull();
            await Assert.That(segment.ElementType).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(segment.ByteOffset).IsEqualTo(32L);
            await Assert.That(segment.Initializers.IsDefault).IsFalse();
            await Assert.That(segment.Initializers.IsEmpty).IsTrue();
        }
    }
}
