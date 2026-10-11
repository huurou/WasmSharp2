using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_BranchIfZeroTests
{
    [Test]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(-1, false)]
    public async Task 条件がゼロまたは非ゼロ_条件を消費しゼロの場合だけ分岐する(
        int condition,
        bool shouldBranch
    )
    {
        // Arrange
        var discarded = WasmValue.FromI32(11);
        var kept = WasmValue.FromF64Bits(0xFFF0123456789ABC);
        WasmValue[] operands = [discarded, kept, WasmValue.FromI32(condition)];
        var instruction = new Instruction(default, default, 81, 1);

        // Act
        var actual = ControlHandlerFixture.Execute(
            Interpreter.BranchIfZero,
            in instruction,
            operands,
            [new(3, 0, 0), new(0, 1, 1)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.After.Pc).IsEqualTo(shouldBranch ? 0 : actual.Before.Pc);
            await Assert.That(actual.After.Function).IsSameReferenceAs(actual.Before.Function);
            await Assert.That(actual.After.StackBase).IsEqualTo(actual.Before.StackBase);
            await Assert.That(actual.After.OperandBase).IsEqualTo(actual.Before.OperandBase);
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(actual.Values.Length).IsEqualTo(shouldBranch ? 3 : 4);
            await Assert.That(actual.Values[0]).IsEqualTo(ControlHandlerFixture.Local);
            await Assert.That(actual.Values[1]).IsEqualTo(ControlHandlerFixture.Sentinel);
            await Assert.That(actual.Values[^1]).IsEqualTo(kept);
            if (!shouldBranch)
            {
                await Assert.That(actual.Values[2]).IsEqualTo(discarded);
            }
        }
    }
}
