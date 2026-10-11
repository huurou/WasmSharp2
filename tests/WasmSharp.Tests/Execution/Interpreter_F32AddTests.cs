using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32AddTests
{
    [Test]
    [Arguments(0x3FC00000u, 0x40100000u, 0x40700000u)]
    [Arguments(0x4B800000u, 0x3F800000u, 0x4B800000u)]
    [Arguments(0x4B800001u, 0x3F800000u, 0x4B800002u)]
    [Arguments(0x80000000u, 0x80000000u, 0x80000000u)]
    [Arguments(0x00000001u, 0x00000001u, 0x00000002u)]
    [Arguments(0x7F7FFFFFu, 0x7F7FFFFFu, 0x7F800000u)]
    [Arguments(0x7F800000u, 0xFF800000u, 0x7FC00000u)]
    [Arguments(0x7FC00000u, 0x3F800000u, 0x7FC00000u)]
    [Arguments(0xFFC12345u, 0x3F800000u, 0x7FC00000u)]
    [Arguments(0x3F800000u, 0x7F812345u, 0x7FC00000u)]
    public async Task 通常値と境界値とNaN_32bitの加算結果を返す(
        uint leftBits,
        uint rightBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(leftBits), WasmValue.FromF32Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Add, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    public async Task 加算後に減算する場合_各命令の丸めを保つ()
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromF32Bits(0x4B800000u),
            WasmValue.FromF32Bits(0x3F800000u),
        ];

        // Act
        var actual = NumericHandlerFixture.Execute(
            static (InterpreterContext context, in Instruction instruction) =>
            {
                var result = Interpreter.F32Add(context, in instruction);
                if (result.Status != ExecutionStatus.Success)
                {
                    return result;
                }

                context.PushValue(WasmValue.FromF32Bits(0x4B800000u));
                return Interpreter.F32Sub(context, in instruction);
            },
            operands
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(0x00000000u);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
