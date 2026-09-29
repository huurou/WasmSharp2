using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetExternrefNumberTests
{
    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    public async Task 割り当てたexternrefを指定する_元の番号を返す(uint number)
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var externref = state.GetExternref(number);

        // Act
        var actual = state.GetExternrefNumber(externref);

        // Assert
        await Assert.That(actual).IsEqualTo(number);
    }

    [Test]
    public async Task 割り当てていない参照を指定する_nullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var other = new ScriptState("b.wast").GetExternref(1);
        state.GetExternref(1);

        // Act
        var unknown = state.GetExternrefNumber(new object());
        var fromOtherInput = state.GetExternrefNumber(other);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(unknown).IsNull();
            await Assert.That(fromOtherInput).IsNull();
        }
    }
}
