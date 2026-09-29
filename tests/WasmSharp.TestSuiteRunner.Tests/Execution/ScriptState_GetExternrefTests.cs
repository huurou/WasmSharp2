using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetExternrefTests
{
    [Test]
    public async Task 同じ番号を繰り返し指定する_同一のobjectを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = state.GetExternref(1);

        // Act
        var second = state.GetExternref(1);

        // Assert
        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public async Task 異なる番号を指定する_別のobjectを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = state.GetExternref(0);

        // Act
        var second = state.GetExternref(uint.MaxValue);

        // Assert
        await Assert.That(second).IsNotSameReferenceAs(first);
    }

    [Test]
    public async Task 別の入力の状態で同じ番号を指定する_以前の入力とは別のobjectを返す()
    {
        // Arrange
        var previous = new ScriptState("a.wast").GetExternref(1);
        var state = new ScriptState("b.wast");

        // Act
        var externref = state.GetExternref(1);

        // Assert
        await Assert.That(externref).IsNotSameReferenceAs(previous);
    }
}
