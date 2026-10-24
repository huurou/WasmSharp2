using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_RefFuncTests
{
    [Test]
    [Arguments(0U)]
    [Arguments(1U)]
    [Arguments(2U)]
    public async Task 別instanceからimportした定義関数で実行する_所属instanceの関数表から同じ関数実体を積む(
        uint index
    )
    {
        // Arrange
        var host = WasmFunction.CreateHost(new([], []), _ => new([]));
        var provider = ExecutionInstanceFixture.Create([new([], [])], [(0, [], [], 1)], [], []);
        var source = ExecutionInstanceFixture.Create(
            [new([], [])],
            [(0, [], [], 1)],
            [host, provider.Functions[0]],
            []
        );
        var current = (DefinedFunction)source.Functions[2];
        var importer = ExecutionInstanceFixture.Create(
            [new([], [])],
            [(0, [], [], 1)],
            [current],
            []
        );
        var instruction = new Instruction(default, default, 1001, index);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        WasmValue actual;
        int valueCount;

        // Act
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(0, 1, (DefinedFunction)importer.Functions[1]);
            context.EnterFrame((DefinedFunction)importer.Functions[1], 0);
            context.EnsureCapacity(0, 1, current);
            context.EnterFrame(current, 0);

            result = Interpreter.RefFunc(context, in instruction);
            actual = context.GetValue(0);
            valueCount = context.ValueCount;
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(valueCount).IsEqualTo(1);
            await Assert.That(actual.Kind).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(actual.AsFuncRef()).IsSameReferenceAs(source.Functions[(int)index]);
        }
    }
}
