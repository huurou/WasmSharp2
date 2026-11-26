using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetHostValueNumberTests
{
    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    public async Task 割り当てたホスト値を指定する_元の番号を返す(uint number)
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var hostValue = state.GetHostValue(number);

        // Act
        var actual = state.GetHostValueNumber(hostValue);

        // Assert
        await Assert.That(actual).IsEqualTo(number);
    }

    [Test]
    public async Task 割り当てていない参照を指定する_nullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var other = new ScriptState("b.wast").GetHostValue(1);
        state.GetHostValue(1);

        // Act
        var unknown = state.GetHostValueNumber(new object());
        var fromOtherInput = state.GetHostValueNumber(other);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(unknown).IsNull();
            await Assert.That(fromOtherInput).IsNull();
        }
    }
}
