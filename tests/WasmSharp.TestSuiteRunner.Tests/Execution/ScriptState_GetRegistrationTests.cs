using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetRegistrationTests
{
    [Test]
    public async Task 一度も登録していない名前を指定する_既知の失敗と区別してnullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        state.Fail(new RegisterCommand(0, 1, null, "M"), null);

        // Act
        var missing = state.GetRegistration("N");
        var failed = state.GetRegistration("M");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(missing).IsNull();
            await Assert.That(failed?.Cause).IsNotNull();
        }
    }

    [Test]
    public async Task 別の入力で登録した名前を指定する_以前の入力の登録を持ち込まずnullを返す()
    {
        // Arrange
        var previous = new ScriptState("a.wast");
        previous.Register(new WasmHostModule("M"));
        var state = new ScriptState("b.wast");

        // Act
        var binding = state.GetRegistration("M");

        // Assert
        await Assert.That(binding).IsNull();
    }
}
