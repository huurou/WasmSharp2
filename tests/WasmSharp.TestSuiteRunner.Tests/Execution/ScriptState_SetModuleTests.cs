using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_SetModuleTests
{
    [Test]
    public async Task 識別子付きの通常moduleが成功する_直近moduleと識別子を同じ成功実体へ更新する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var module = ModuleFixture.Instantiate();

        // Act
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(module);
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
            await Assert.That(state.ResolveModule(null)?.Value).IsSameReferenceAs(module);
        }
    }

    [Test]
    public async Task 識別子なしの通常moduleが成功する_直近moduleだけを更新し既存の識別子を保持する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var first = ModuleFixture.Instantiate();
        var second = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), first);

        // Act
        state.SetModule(ModuleFixture.CreateCommand(1, null), second);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(second);
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(first);
        }
    }

    [Test]
    public async Task 利用不能な識別子へ通常moduleが成功する_新しい成功実体へ置換する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var module = ModuleFixture.Instantiate();
        state.Fail(ModuleFixture.CreateCommand(0, "$M"), null);

        // Act
        state.SetModule(ModuleFixture.CreateCommand(1, "$M"), module);

        // Assert
        var binding = state.ResolveModule("$M");
        using (Assert.Multiple())
        {
            await Assert.That(binding?.Value).IsSameReferenceAs(module);
            await Assert.That(binding?.Cause).IsNull();
        }
    }

    [Test]
    public async Task 登録名と同じ文字列の識別子で成功する_登録名の状態を作らない()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        state.SetModule(ModuleFixture.CreateCommand(0, "M"), ModuleFixture.Instantiate());

        // Assert
        await Assert.That(state.GetRegistration("M")).IsNull();
    }
}
