using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_RegisterTests
{
    [Test]
    public async Task 同じ登録名へ再登録する_後の提供元へ置換する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = new WasmHostModule("M");
        var second = new WasmHostModule("M");
        state.Register(first);

        // Act
        state.Register(second);

        // Assert
        await Assert.That(state.GetRegistration("M")?.Value).IsSameReferenceAs(second);
    }

    [Test]
    public async Task 利用不能な登録名へ再登録する_成功した提供元へ置換する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var module = new WasmHostModule("M");
        state.Fail(new RegisterCommand(0, 1, null, "M"), null);

        // Act
        state.Register(module);

        // Assert
        var binding = state.GetRegistration("M");
        using (Assert.Multiple())
        {
            await Assert.That(binding?.Value).IsSameReferenceAs(module);
            await Assert.That(binding?.Cause).IsNull();
        }
    }

    [Test]
    public async Task Module識別子と同じ文字列の登録名で登録する_module識別子の解決を変更しない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var module = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);

        // Act
        state.Register(new WasmHostModule("$M"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(module);
        }
    }
}
