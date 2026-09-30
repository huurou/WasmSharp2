using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 照合済みの素材と入力の状態を使い、commandを順序どおりに実行する
/// </summary>
/// <param name="input">この入力の照合済みbinaryと素材異常</param>
internal sealed class ScriptExecutor(InputVerification input)
{
    /// <summary>
    /// すべてのinstanceへ明示する呼び出し深さの上限
    /// </summary>
    internal const int MAX_CALL_DEPTH = 1024;

    /// <summary>
    /// 全commandを一度ずつ処理し、公開操作の観測から結果を記録する。
    /// </summary>
    /// <param name="script">同じ照合済みdocumentから読み取ったcommand</param>
    /// <param name="state">この入力のmodule・登録・printの状態</param>
    /// <returns>commandの順序を保つケース結果の配列 個々の失敗を記録して後続commandを続行する</returns>
    internal ImmutableArray<CaseResult> Execute(ScriptReadResult script, ScriptState state)
    {
        return [.. script.Commands.Select(x => Execute(x, state))];
    }

    /// <summary>
    /// 一つのcommandを実行し、失敗した通常moduleと登録の対象を原因付きで利用不能にする。
    /// </summary>
    /// <param name="command">実行と判定を行う1件のcommand</param>
    /// <param name="state">この入力の名前と参照の対応 現在のcommandのprint・callback記録を初期化して使う</param>
    /// <returns>観測した段階・値・例外・printを持つケース結果 command処理中の例外は診断として記録する</returns>
    internal CaseResult Execute(ScriptCommand command, ScriptState state)
    {
        var observation = new CommandObservation(
            state.BeginCommand(command.Index),
            "read_command",
            null
        );
        try
        {
            switch (command)
            {
                case ModuleCommand module:
                    ExecuteModule(command, module.ModuleType, state, ref observation);
                    break;
                case ModuleAssertionCommand assertion:
                    ExecuteModule(command, assertion.ModuleType, state, ref observation);
                    break;
                case RegisterCommand registration:
                    ExecuteRegister(registration, state, ref observation);
                    break;
                case ActionCommand action:
                    ExecuteAction(action.Action, [], false, state, ref observation);
                    break;
                case AssertReturnCommand assertion:
                    ExecuteAction(
                        assertion.Action,
                        assertion.Expected,
                        true,
                        state,
                        ref observation
                    );
                    break;
                case AssertTrapCommand assertion:
                    ExecuteAction(assertion.Action, [], false, state, ref observation);
                    break;
                case AssertExhaustionCommand assertion:
                    ExecuteAction(assertion.Action, [], false, state, ref observation);
                    break;
            }
        }
        catch (Exception exception)
        {
            observation = observation with { Exception = exception };
        }
        observation = observation with
        {
            Prints = state.Prints,
            CallbackException = state.CallbackException,
        };
        var result = AssertionJudge.Judge(command, observation);
        if (result.Outcome != CaseOutcome.Passed)
        {
            state.Fail(command, result.Cause);
        }
        return result;
    }

    /// <summary>
    /// 素材・Decode・Validateを先に処理し、Instantiateへ進む場合だけ登録依存を調べる。
    /// </summary>
    /// <param name="command">通常moduleまたはmoduleを対象とする否定assertion</param>
    /// <param name="type">素材の形式 textの場合は素材を開かず復帰する</param>
    /// <param name="state">登録依存を解決する入力の状態 通常moduleの成功時だけ直近moduleと識別子を更新する</param>
    /// <param name="observation">最後の処理段階、素材の異常、登録依存の失敗を反映する観測</param>
    private void ExecuteModule(
        ScriptCommand command,
        ScriptModuleType type,
        ScriptState state,
        ref CommandObservation observation
    )
    {
        if (type == ScriptModuleType.Text)
        {
            return;
        }
        observation = observation with { Operation = "verify_module" };
        if (input.ModuleIssues.TryGetValue(command.Index, out var issue))
        {
            observation = observation with
            {
                Diagnostics =
                [
                    new(issue.Operation, issue.Message)
                    {
                        Path = issue.Path,
                        ExceptionType = issue.ExceptionType,
                    },
                ],
            };
            return;
        }
        var bytes = input.Modules[command.Index];
        observation = observation with { Operation = "decode", LastStage = CaseStage.Decode };
        var module = WasmModule.Decode(bytes.AsSpan());
        if (command is AssertMalformedCommand)
        {
            return;
        }
        observation = observation with { Operation = "validate", LastStage = CaseStage.Validate };
        module.Validate();
        if (command is AssertInvalidCommand)
        {
            return;
        }
        observation = observation with
        {
            Operation = "inspect_imports",
            LastStage = CaseStage.InspectImports,
        };
        var inspection = WasmModule.InspectImports(bytes.AsSpan());
        var causes = inspection
            .Imports.Select(x => state.GetRegistration(x.ModuleName)?.Cause)
            .OfType<UnavailableCause>()
            .Distinct()
            .ToImmutableArray();
        if (!causes.IsEmpty)
        {
            observation = observation with { BlockedBy = causes };
            return;
        }
        observation = observation with
        {
            Operation = "instantiate",
            LastStage = CaseStage.Instantiate,
        };
        var instance = module.Instantiate(
            state.CreateImports(),
            new WasmExecutionOptions(MAX_CALL_DEPTH)
        );
        if (command is ModuleCommand normal)
        {
            state.SetModule(normal, new(module, instance));
        }
    }

    /// <summary>
    /// 公開export一覧と名前取得APIで同じ実体を提供し、登録名の対応を置き換える。
    /// </summary>
    /// <param name="command">提供元moduleと登録名を指定するregister</param>
    /// <param name="state">moduleを解決し、成功時に最新の登録を保持する入力の状態</param>
    /// <param name="observation">moduleの不在・利用不能状態またはRegister段階を反映する観測</param>
    private static void ExecuteRegister(
        RegisterCommand command,
        ScriptState state,
        ref CommandObservation observation
    )
    {
        observation = observation with { Operation = "resolve_module" };
        var binding = ResolveModule(command.Name, state, ref observation);
        if (binding is null)
        {
            return;
        }
        observation = observation with { Operation = "register", LastStage = CaseStage.Register };
        var host = new WasmHostModule(command.As);
        foreach (var export in binding.Module.GetExports())
        {
            switch (export.Kind)
            {
                case WasmExternalKind.Function:
                    host.Define(export.Name, binding.Instance.GetFunction(export.Name));
                    break;
                case WasmExternalKind.Global:
                    host.Define(export.Name, binding.Instance.GetGlobalResource(export.Name));
                    break;
                case WasmExternalKind.Memory:
                    host.Define(export.Name, binding.Instance.GetMemory(export.Name));
                    break;
                case WasmExternalKind.Table:
                    host.Define(export.Name, binding.Instance.GetTable(export.Name));
                    break;
            }
        }
        state.Register(host);
    }

    /// <summary>
    /// module参照の不在をスクリプト異常とし、既知の失敗だけをblockedの原因にする。
    /// </summary>
    /// <param name="name">対象moduleの識別子 省略時は直近の通常moduleを示すnull</param>
    /// <param name="state">module識別子と直近moduleを保持する入力の状態</param>
    /// <param name="observation">対象が不在の場合は診断、利用不能の場合は依存する原因を反映する観測</param>
    /// <returns>成功したmoduleとinstance 不在または利用不能の場合はnull</returns>
    private static InstantiatedModule? ResolveModule(
        string? name,
        ScriptState state,
        ref CommandObservation observation
    )
    {
        var binding = state.ResolveModule(name);
        if (binding is null)
        {
            observation = observation with
            {
                Diagnostics =
                [
                    new("resolve_module", $"対象module {name ?? "(直近)"}がありません。"),
                ],
            };
        }
        else if (binding.Cause is { } cause)
        {
            observation = observation with { BlockedBy = [cause] };
        }
        return binding?.Value;
    }

    /// <summary>
    /// 引数と値付き期待値を先に検査し、invoke/getの結果を記録・比較する。
    /// </summary>
    /// <param name="action">対象moduleとexport名を持つ公開操作</param>
    /// <param name="expected">assert_returnの値付き期待値 それ以外は空</param>
    /// <param name="compareValues">assert_returnの場合だけ値を比較する</param>
    /// <param name="state">参照の同一性とmodule参照を保持する入力の状態</param>
    /// <param name="observation">実行済みの操作と、例外・値比較を渡す観測</param>
    private static void ExecuteAction(
        ScriptAction action,
        ImmutableArray<ExpectedValue> expected,
        bool compareValues,
        ScriptState state,
        ref CommandObservation observation
    )
    {
        observation = observation with { Operation = "create_arguments" };
        var arguments = action is InvokeAction invoke
            ? ValueCodec.CreateArguments(invoke.Arguments, state)
            : [];
        observation = observation with { Operation = "parse_expected" };
        var patterns = ValueMatcher.Parse(expected);
        observation = observation with { Operation = "resolve_module" };
        var module = ResolveModule(action.Module, state, ref observation);
        if (module is null)
        {
            return;
        }
        observation = observation with
        {
            Operation = action is InvokeAction ? "invoke" : "get",
            LastStage = action is InvokeAction ? CaseStage.Invoke : CaseStage.Get,
        };
        var actual =
            action is InvokeAction
                ? module.Instance.GetFunction(action.Field).Invoke(arguments.AsSpan())
                : new WasmResults([module.Instance.GetGlobal(action.Field)]);
        observation = observation with { Operation = "record_values" };
        observation = observation with
        {
            Values = [.. actual.Values.Select(x => ValueCodec.Record(x, state))],
        };
        if (compareValues)
        {
            observation = observation with { Operation = "match_values" };
            observation = observation with
            {
                Mismatches = ValueMatcher.Match(patterns, actual, state),
            };
        }
    }
}
