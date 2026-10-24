using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_RefIsNullTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 参照値を判定する_nullはi32の1で非nullはi32の0へ置き換える(
        WasmValueKind kind,
        bool isNull
    )
    {
        // Arrange
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(isNull ? null : FunctionFixture.Create())
                : WasmValue.FromExternRef(isNull ? null : new object());
        var instruction = new Instruction(default, default, 1001, 0);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        WasmValue actual;
        int valueCount;

        // Act
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(0, 1, FunctionFixture.Create());
            context.PushValue(value);

            result = Interpreter.RefIsNull(context, in instruction);
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
            await Assert.That(actual.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.AsI32()).IsEqualTo(isNull ? 1 : 0);
        }
    }
}
