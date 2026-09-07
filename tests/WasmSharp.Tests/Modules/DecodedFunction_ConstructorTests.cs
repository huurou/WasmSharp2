using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class DecodedFunction_ConstructorTests
{
    [Test]
    public async Task 元の配列を変更する_圧縮localsと命令と元位置を独立して保持する()
    {
        // Arrange
        LocalDeclaration[] locals = [new(uint.MaxValue, WasmValueKind.I64)];
        DecodedInstruction[] instructions =
        [
            new(new(0, 0x44), WasmValue.FromF64Bits(0xFFF8123456789ABCUL), 37),
            new(new(0, 0x0B), default, 46),
        ];

        // Act
        var function = new DecodedFunction(uint.MaxValue, 30, locals, instructions);
        Array.Clear(locals);
        Array.Clear(instructions);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(function.TypeIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(function.BodyOffset).IsEqualTo(30L);
            await Assert.That(function.Locals.Length).IsEqualTo(1);
            await Assert.That(function.Locals[0].Count).IsEqualTo(uint.MaxValue);
            await Assert.That(function.Locals[0].Type).IsEqualTo(WasmValueKind.I64);
            await Assert.That(function.Instructions.Length).IsEqualTo(2);
            await Assert.That(function.Instructions[0].Opcode).IsEqualTo(new OpcodeKey(0, 0x44));
            await Assert
                .That(function.Instructions[0].Immediate.AsF64Bits())
                .IsEqualTo(0xFFF8123456789ABCUL);
            await Assert.That(function.Instructions[0].ByteOffset).IsEqualTo(37L);
            await Assert.That(function.Instructions[1].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
            await Assert.That(function.Instructions[1].ByteOffset).IsEqualTo(46L);
        }
    }
}
