using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64AddTests
{
    [Test]
    [Arguments(0x3FF8000000000000ul, 0x4002000000000000ul, 0x400E000000000000ul)]
    [Arguments(0x4340000000000000ul, 0x3FF0000000000000ul, 0x4340000000000000ul)]
    [Arguments(0x4340000000000001ul, 0x3FF0000000000000ul, 0x4340000000000002ul)]
    [Arguments(0x8000000000000000ul, 0x8000000000000000ul, 0x8000000000000000ul)]
    [Arguments(0x0000000000000001ul, 0x0000000000000001ul, 0x0000000000000002ul)]
    [Arguments(0x7FEFFFFFFFFFFFFFul, 0x7FEFFFFFFFFFFFFFul, 0x7FF0000000000000ul)]
    [Arguments(0x7FF0000000000000ul, 0xFFF0000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0x7FF8000000000000ul, 0x3FF0000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xFFF8123456789ABCul, 0x3FF0000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0x3FF0000000000000ul, 0x7FF0123456789ABCul, 0x7FF8000000000000ul)]
    public async Task 通常値と境界値とNaN_64bitの加算結果を返す(
        ulong leftBits,
        ulong rightBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(leftBits), WasmValue.FromF64Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64Add, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(expectedBits);
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
            WasmValue.FromF64Bits(0x4340000000000000ul),
            WasmValue.FromF64Bits(0x3FF0000000000000ul),
        ];

        // Act
        var actual = NumericHandlerFixture.Execute(
            static (InterpreterContext context, in Instruction instruction) =>
            {
                var result = Interpreter.F64Add(context, in instruction);
                if (result.Status != ExecutionStatus.Success)
                {
                    return result;
                }

                context.PushValue(WasmValue.FromF64Bits(0x4340000000000000ul));
                return Interpreter.F64Sub(context, in instruction);
            },
            operands
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(0x0000000000000000ul);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
