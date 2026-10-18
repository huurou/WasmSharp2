using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules.Definitions;

internal class ElementDefinition_DeclarativeTests
{
    [Test]
    public async Task 元の命令配列を変更する_対象を持たず未評価の初期化式と位置を保持する()
    {
        // Arrange
        DecodedInstruction[] instructions =
        [
            DecodedInstruction.WithIndex(new(0, 0xD2), 7, 61),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 63),
        ];
        IEnumerable<DecodedInstruction>[] initializers = [instructions];

        // Act
        var segment = ElementDefinition.Declarative(WasmValueKind.FuncRef, initializers, 58);
        Array.Clear(instructions);
        Array.Clear(initializers);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segment.Mode).IsEqualTo(ElementMode.Declarative);
            await Assert.That(segment.ActiveTarget).IsNull();
            await Assert.That(segment.ElementType).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(segment.ByteOffset).IsEqualTo(58L);
            await Assert.That(segment.Initializers.Length).IsEqualTo(1);
            await Assert.That(segment.Initializers[0][0].Index).IsEqualTo(7u);
            await Assert.That(segment.Initializers[0][0].ByteOffset).IsEqualTo(61L);
            await Assert.That(segment.Initializers[0][1].ByteOffset).IsEqualTo(63L);
        }
    }
}
