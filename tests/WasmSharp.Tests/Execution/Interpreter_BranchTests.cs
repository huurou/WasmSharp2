using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_BranchTests
{
    [Test]
    [Arguments(1U, 3)]
    [Arguments(2U, 0)]
    public async Task 前方または後方へ繰り返し分岐する_保持値と下位値を保ち値数と深さを増やさない(
        uint index,
        int expectedPc
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromI32(11),
            WasmValue.FromF32Bits(0xFF812345),
            WasmValue.FromF64Bits(0x8000000000000000),
        ];
        var instruction = new Instruction(default, default, 81, index);
        BranchTarget[] targets = [new(1, 0, 0), new(3, 1, 2), new(0, 1, 2)];

        // Act
        var actual = ControlHandlerFixture.Execute(
            RepeatBranch,
            in instruction,
            operands,
            [.. targets]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.After.Pc).IsEqualTo(expectedPc);
            await Assert.That(actual.After.Function).IsSameReferenceAs(actual.Before.Function);
            await Assert.That(actual.After.StackBase).IsEqualTo(actual.Before.StackBase);
            await Assert.That(actual.After.OperandBase).IsEqualTo(actual.Before.OperandBase);
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(actual.Values.Length).IsEqualTo(4);
            await Assert.That(actual.Values[0]).IsEqualTo(ControlHandlerFixture.Local);
            await Assert.That(actual.Values[1]).IsEqualTo(ControlHandlerFixture.Sentinel);
            await Assert.That(actual.Values[2]).IsEqualTo(operands[1]);
            await Assert.That(actual.Values[3]).IsEqualTo(operands[2]);
        }
    }

    private static ExecutionResult RepeatBranch(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        ExecutionResult result = default;
        for (var i = 0; i < 32; i++)
        {
            result = Interpreter.Branch(context, in instruction);
        }
        return result;
    }
}
