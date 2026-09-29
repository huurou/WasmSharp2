using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_FailTests
{
    private const string INPUT_PATH = "a.wast";

    [Test]
    public async Task 成功後に同じ識別子の通常moduleが失敗する_直近moduleと識別子を原因付きで利用不能にし以前の成功へ戻さない()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), ModuleFixture.Instantiate());

        // Act
        state.Fail(ModuleFixture.CreateCommand(1, "$M"), null);

        // Assert
        var last = state.LastModule;
        var named = state.ResolveModule("$M");
        using (Assert.Multiple())
        {
            await Assert.That(last?.Value).IsNull();
            await Assert.That(last?.Cause?.Command).IsEqualTo(Id(1));
            await Assert.That(last?.Cause?.Origins.SequenceEqual([Id(1)])).IsTrue();
            await Assert.That(named?.Value).IsNull();
            await Assert.That(named?.Cause).IsEqualTo(last?.Cause);
            await Assert.That(state.ResolveModule(null)?.Value).IsNull();
        }
    }

    [Test]
    public async Task 識別子なしの通常moduleが失敗する_直近moduleだけを利用不能にし既存の識別子を保持する()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var module = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);

        // Act
        state.Fail(ModuleFixture.CreateCommand(1, null), null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Cause?.Command).IsEqualTo(Id(1));
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
        }
    }

    [Test]
    public async Task 成功後にregisterが失敗する_登録名を原因付きで利用不能にし以前の提供元へ戻さない()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        state.Register(new WasmHostModule("M"));

        // Act
        state.Fail(new RegisterCommand(1, 2, "$M", "M"), null);

        // Assert
        var binding = state.GetRegistration("M");
        using (Assert.Multiple())
        {
            await Assert.That(binding?.Value).IsNull();
            await Assert.That(binding?.Cause?.Command).IsEqualTo(Id(1));
            await Assert.That(binding?.Cause?.Origins.SequenceEqual([Id(1)])).IsTrue();
        }
    }

    [Test]
    public async Task Blockedのcommandが順に失敗する_直接原因は自身とし元の失敗を引き継ぐ()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        state.Fail(ModuleFixture.CreateCommand(0, "$A"), null);
        var registerCause = UnavailableCause.CreateCaseCause([state.ResolveModule("$A")!.Cause!]);

        // Act
        state.Fail(new RegisterCommand(1, 2, "$A", "A"), registerCause);
        var moduleCause = UnavailableCause.CreateCaseCause([state.GetRegistration("A")!.Cause!]);
        state.Fail(ModuleFixture.CreateCommand(2, "$B"), moduleCause);

        // Assert
        var registration = state.GetRegistration("A")?.Cause;
        var module = state.ResolveModule("$B")?.Cause;
        using (Assert.Multiple())
        {
            await Assert.That(registration?.Command).IsEqualTo(Id(1));
            await Assert.That(registration?.Origins.SequenceEqual([Id(0)])).IsTrue();
            await Assert.That(moduleCause.Direct.SequenceEqual([Id(1)])).IsTrue();
            await Assert.That(moduleCause.Origins.SequenceEqual([Id(0)])).IsTrue();
            await Assert.That(module?.Command).IsEqualTo(Id(2));
            await Assert.That(module?.Origins.SequenceEqual([Id(0)])).IsTrue();
        }
    }

    [Test]
    public async Task 識別子を読み取れた不正な通常moduleが失敗する_直近moduleとその識別子だけを利用不能にする()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var other = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), ModuleFixture.Instantiate());
        state.SetModule(ModuleFixture.CreateCommand(1, "$N"), other);

        // Act
        state.Fail(Invalid(2, ScriptCommand.MODULE) with { Name = "$M" }, null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Cause?.Command).IsEqualTo(Id(2));
            await Assert.That(state.ResolveModule("$M")?.Cause?.Command).IsEqualTo(Id(2));
            await Assert.That(state.ResolveModule("$N")?.Value).IsSameReferenceAs(other);
        }
    }

    [Test]
    public async Task 識別子を取得できない不正な通常moduleが失敗する_直近moduleだけを利用不能にし識別子を推定しない()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var module = ModuleFixture.Instantiate();
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);

        // Act
        state.Fail(Invalid(1, ScriptCommand.MODULE), null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Cause?.Command).IsEqualTo(Id(1));
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
        }
    }

    [Test]
    public async Task 登録名を読み取れた不正なregisterが失敗する_その登録名だけを利用不能にする()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var module = ModuleFixture.Instantiate();
        var other = new WasmHostModule("N");
        state.SetModule(ModuleFixture.CreateCommand(0, null), module);
        state.Register(new WasmHostModule("M"));
        state.Register(other);

        // Act
        state.Fail(Invalid(1, ScriptCommand.REGISTER) with { As = "M" }, null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.GetRegistration("M")?.Cause?.Command).IsEqualTo(Id(1));
            await Assert.That(state.GetRegistration("N")?.Value).IsSameReferenceAs(other);
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(module);
        }
    }

    [Test]
    [Arguments(ScriptCommand.REGISTER)]
    [Arguments("assert_exception")]
    [Arguments(null)]
    public async Task 更新対象を取得できない不正なcommandが失敗する_名前状態を更新しない(
        string? type
    )
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var module = ModuleFixture.Instantiate();
        var registration = new WasmHostModule("M");
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);
        state.Register(registration);

        // Act
        state.Fail(Invalid(1, type), null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(module);
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
            await Assert.That(state.GetRegistration("M")?.Value).IsSameReferenceAs(registration);
        }
    }

    [Test]
    public async Task 否定moduleとactionとassertionが失敗する_名前状態を更新しない()
    {
        // Arrange
        var state = new ScriptState(INPUT_PATH);
        var module = ModuleFixture.Instantiate();
        var registration = new WasmHostModule("M");
        state.SetModule(ModuleFixture.CreateCommand(0, "$M"), module);
        state.Register(registration);
        var action = new InvokeAction("$M", "f", []);
        ScriptCommand[] commands =
        [
            new AssertMalformedCommand(1, 2, "a.1.wasm", "x", ScriptModuleType.Binary),
            new AssertInvalidCommand(2, 3, "a.2.wasm", "x", ScriptModuleType.Binary),
            new AssertUnlinkableCommand(3, 4, "a.3.wasm", "x", ScriptModuleType.Binary),
            new AssertUninstantiableCommand(4, 5, "a.4.wasm", "x", ScriptModuleType.Binary),
            new ActionCommand(5, 6, action, []),
            new AssertReturnCommand(6, 7, action, []),
            new AssertTrapCommand(7, 8, action, "x", []),
            new AssertExhaustionCommand(8, 9, action, "x", []),
        ];

        // Act
        foreach (var command in commands)
        {
            state.Fail(command, null);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.LastModule?.Value).IsSameReferenceAs(module);
            await Assert.That(state.ResolveModule("$M")?.Value).IsSameReferenceAs(module);
            await Assert.That(state.GetRegistration("M")?.Value).IsSameReferenceAs(registration);
        }
    }

    private static InvalidCommand Invalid(int index, string? type)
    {
        return new(index, index + 1, type, new("read_command", "不正なcommandです。"));
    }

    private static CaseId Id(int index)
    {
        return new(INPUT_PATH, index);
    }
}
