using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules.Definitions;

internal class ActiveElementTarget_ConstructorTests
{
    [Test]
    public async Task 元の命令配列を変更する_未検証のtable添字とオフセット式と入力位置を独立して保持する()
    {
        // Arrange
        DecodedInstruction[] expression =
        [
            DecodedInstruction.WithIndex(new(0, 0x23), uint.MaxValue, 20),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 26),
        ];

        // Act
        var target = new ActiveElementTarget(uint.MaxValue, expression);
        Array.Clear(expression);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(target.TableIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(target.OffsetExpression.Length).IsEqualTo(2);
            await Assert.That(target.OffsetExpression[0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(target.OffsetExpression[0].ByteOffset).IsEqualTo(20L);
            await Assert.That(target.OffsetExpression[1].ByteOffset).IsEqualTo(26L);
        }
    }
}
