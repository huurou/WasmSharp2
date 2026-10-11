using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_BranchTableTests
{
    [Test]
    [Arguments(0, 2, 3)]
    [Arguments(1, 2, 0)]
    [Arguments(2, 2, 1)]
    [Arguments(-1, 2, 1)]
    [Arguments(int.MinValue, 2, 1)]
    [Arguments(int.MaxValue, 2, 1)]
    [Arguments(0, 0, 1)]
    public async Task 選択値と候補数に応じて分岐する_候補順または既定先を選び分岐値だけを保持する(
        int selector,
        int targetCount,
        int expectedPc
    )
    {
        // Arrange
        var kept = WasmValue.FromI64(long.MinValue);
        WasmValue[] operands = [WasmValue.FromI32(11), kept, WasmValue.FromI32(selector)];
        var instruction = new Instruction(default, default, 81, 1, targetCount);
        BranchTarget[] targets =
            targetCount == 0
                ? [new(2, 0, 0), new(1, 1, 1)]
                : [new(2, 0, 0), new(3, 1, 1), new(0, 1, 1), new(1, 1, 1)];

        // Act
        var actual = ControlHandlerFixture.Execute(
            Interpreter.BranchTable,
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
            await Assert.That(actual.Values.Length).IsEqualTo(3);
            await Assert.That(actual.Values[0]).IsEqualTo(ControlHandlerFixture.Local);
            await Assert.That(actual.Values[1]).IsEqualTo(ControlHandlerFixture.Sentinel);
            await Assert.That(actual.Values[2]).IsEqualTo(kept);
        }
    }
}
