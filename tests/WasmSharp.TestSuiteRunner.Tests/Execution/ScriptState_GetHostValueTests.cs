using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetHostValueTests
{
    [Test]
    public async Task 同じ番号を繰り返し指定する_同じホスト値を返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = state.GetHostValue(1);

        // Act
        var second = state.GetHostValue(1);

        // Assert
        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public async Task 異なる番号を指定する_別のホスト値を返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = state.GetHostValue(0);

        // Act
        var second = state.GetHostValue(uint.MaxValue);

        // Assert
        await Assert.That(second).IsNotSameReferenceAs(first);
    }

    [Test]
    public async Task 別の入力の状態で同じ番号を指定する_以前の入力とは別のホスト値を返す()
    {
        // Arrange
        var previous = new ScriptState("a.wast").GetHostValue(1);
        var state = new ScriptState("b.wast");

        // Act
        var hostValue = state.GetHostValue(1);

        // Assert
        await Assert.That(hostValue).IsNotSameReferenceAs(previous);
    }
}
