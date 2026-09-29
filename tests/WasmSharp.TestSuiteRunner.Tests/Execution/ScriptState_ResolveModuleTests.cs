using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_ResolveModuleTests
{
    [Test]
    public async Task 通常moduleを処理する前に識別子を省略する_nullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var binding = state.ResolveModule(null);

        // Assert
        await Assert.That(binding).IsNull();
    }

    [Test]
    public async Task 存在しない識別子を指定する_直近moduleで代替せずnullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), ModuleFixture.Instantiate());

        // Act
        var binding = state.ResolveModule("$N");

        // Assert
        await Assert.That(binding).IsNull();
    }

    [Test]
    public async Task 識別子を指定する_直近moduleではなく指定した識別子のmoduleを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var named = ModuleFixture.Instantiate();
        var last = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), named);
        state.SetModule(ModuleFixture.CreateCommand(1, "$N"), last);

        // Act
        var binding = state.ResolveModule("$M");

        // Assert
        await Assert.That(binding?.Value).IsSameReferenceAs(named);
    }

    [Test]
    public async Task 識別子を省略する_直近の通常moduleを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var last = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), ModuleFixture.Instantiate());
        state.SetModule(ModuleFixture.CreateCommand(1, null), last);

        // Act
        var binding = state.ResolveModule(null);

        // Assert
        await Assert.That(binding?.Value).IsSameReferenceAs(last);
    }

    [Test]
    public async Task 登録名と同じ文字列の識別子を指定する_登録名をmodule識別子として扱わずnullを返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        state.Register(new WasmHostModule("$M"));

        // Act
        var binding = state.ResolveModule("$M");

        // Assert
        await Assert.That(binding).IsNull();
    }
}
