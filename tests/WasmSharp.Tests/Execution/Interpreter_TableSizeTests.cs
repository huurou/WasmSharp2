using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableSizeTests
{
    [Test]
    [Arguments(0U, 0)]
    [Arguments(1U, 3)]
    public async Task tableを指定する_所属instanceの現在の要素数をi32で積む(
        uint tableIndex,
        int expected
    )
    {
        // Arrange
        var function = TableHandlerFixture.CreateFunction([
            new(WasmValueKind.FuncRef, new(0)),
            new(WasmValueKind.ExternRef, new(3)),
        ]);

        // Act
        var actual = TableHandlerFixture.Execute(Interpreter.TableSize, function, tableIndex, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
        }
    }
}
