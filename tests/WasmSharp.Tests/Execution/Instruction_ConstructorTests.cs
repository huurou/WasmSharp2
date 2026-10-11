using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Tests.Execution;

internal class Instruction_ConstructorTests
{
    [Test]
    public async Task 既存の4引数で構築する_候補数0と命令情報を保持する()
    {
        // Arrange
        var immediate = WasmValue.FromF64Bits(0xFFF8123456789ABC);

        // Act
        var instruction = new Instruction(ExecutionOpcode.Op44, immediate, 12345678901, 7);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Opcode).IsEqualTo(ExecutionOpcode.Op44);
            await Assert.That(instruction.Immediate).IsEqualTo(immediate);
            await Assert.That(instruction.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(instruction.Index).IsEqualTo(7u);
            await Assert.That(instruction.TargetCount).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task 分岐表の開始番号と候補数を指定する_既定を除く候補数を保持する(int targetCount)
    {
        // Arrange
        const uint INDEX = 3;
        const long BYTE_OFFSET = 12345678901;

        // Act
        var instruction = new Instruction(
            ExecutionOpcode.Op0B,
            default,
            BYTE_OFFSET,
            INDEX,
            targetCount
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Index).IsEqualTo(INDEX);
            await Assert.That(instruction.TargetCount).IsEqualTo(targetCount);
            await Assert.That(instruction.ByteOffset).IsEqualTo(BYTE_OFFSET);
        }
    }
}
