using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules.Definitions;

internal class ElementDefinition_ActiveTests
{
    [Test]
    public async Task 元の式配列と命令配列を変更する_型と初期化式と対象と入力位置を独立して保持する()
    {
        // Arrange
        DecodedInstruction[] first =
        [
            DecodedInstruction.WithIndex(new(0, 0xD2), uint.MaxValue, 41),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 46),
        ];
        DecodedInstruction[] second =
        [
            DecodedInstruction.WithImmediate(new(0, 0xD0), WasmValue.FromExternRef(null), 47),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 49),
        ];
        IEnumerable<DecodedInstruction>[] initializers = [first, second];
        var target = new ActiveElementTarget(uint.MaxValue, []);

        // Act
        var segment = ElementDefinition.Active(WasmValueKind.ExternRef, initializers, target, 30);
        Array.Clear(first);
        Array.Clear(second);
        Array.Clear(initializers);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segment.Mode).IsEqualTo(ElementMode.Active);
            await Assert.That(ReferenceEquals(segment.ActiveTarget, target)).IsTrue();
            await Assert.That(segment.ElementType).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(segment.ByteOffset).IsEqualTo(30L);
            await Assert.That(segment.Initializers.Length).IsEqualTo(2);
            await Assert.That(segment.Initializers[0].Length).IsEqualTo(2);
            await Assert.That(segment.Initializers[0][0].Opcode).IsEqualTo(new OpcodeKey(0, 0xD2));
            await Assert.That(segment.Initializers[0][0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(segment.Initializers[0][0].ByteOffset).IsEqualTo(41L);
            await Assert.That(segment.Initializers[0][1].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
            await Assert.That(segment.Initializers[0][1].ByteOffset).IsEqualTo(46L);
            await Assert
                .That(segment.Initializers[1][0].Immediate.Kind)
                .IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(segment.Initializers[1][0].ByteOffset).IsEqualTo(47L);
            await Assert.That(segment.Initializers[1][1].ByteOffset).IsEqualTo(49L);
        }
    }

    [Test]
    public async Task 初期化先がnullである_アクティブ定義を構築しない()
    {
        // Act & Assert
        await Assert
            .That(() => ElementDefinition.Active(WasmValueKind.FuncRef, [], null!, 12))
            .ThrowsExactly<ArgumentNullException>();
    }
}
