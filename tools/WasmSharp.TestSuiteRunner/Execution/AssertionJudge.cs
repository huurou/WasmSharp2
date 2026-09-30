using WasmSharp.Exceptions;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// commandの期待と公開操作の観測から結果を判定する
/// </summary>
internal static class AssertionJudge
{
    /// <summary>
    /// 観測済みのcommandを判定し、保存用のケース結果を返す。
    /// </summary>
    /// <param name="command">期待する操作・値・失敗を持つcommand</param>
    /// <param name="observation">実行側が確定した段階・例外・値比較・依存の観測</param>
    /// <returns>観測を書き換えず、保存用コレクションをコピーした結果</returns>
    internal static CaseResult Judge(ScriptCommand command, CommandObservation observation)
    {
        var fromCallback =
            observation.Exception is not null
            && ReferenceEquals(observation.Exception, observation.CallbackException);
        var diagnostics = observation.Diagnostics.Select(CopyDiagnostic).ToList();
        if (command is InvalidCommand invalid)
        {
            diagnostics.Add(CopyDiagnostic(invalid.Diagnostic));
        }
        if (observation.Exception is { } exception)
        {
            diagnostics.Add(
                CaseDiagnostic.FromException(
                    observation.Operation,
                    observation.LastStage,
                    exception,
                    fromCallback
                )
            );
        }
        diagnostics.AddRange(
            observation.Mismatches.Select(x => new CaseDiagnostic("match_values", x.Message)
            {
                Stage = observation.LastStage,
            })
        );

        var outcome = Classify(command, observation, fromCallback);
        return new(observation.Id, command.Line, command.Type, command.Category, outcome)
        {
            ExpectedText = ExpectedText(command),
            ExpectedValues = ExpectedValues(command),
            ActualValues = [.. observation.Values],
            LastStage = observation.LastStage,
            Diagnostics = diagnostics,
            Prints = [.. observation.Prints.Select(x => x with { Arguments = [.. x.Arguments] })],
            Cause =
                outcome == CaseOutcome.Blocked
                    ? UnavailableCause.CreateCaseCause(observation.BlockedBy)
                    : null,
        };
    }

    /// <summary>
    /// 判定不能の原因を先に区別し、評価できた期待だけを成立または不一致にする。
    /// </summary>
    /// <param name="command">期待する操作・値・失敗を持つcommand</param>
    /// <param name="observation">公開段階、例外、値の相違、依存先の失敗を記録した観測</param>
    /// <param name="fromCallback">観測した例外がspectestのcallbackで発生した同じ例外実体かどうか</param>
    /// <returns>観測と期待から決めた結果分類 callbackの例外や素材・JSONの異常はrunner_error</returns>
    private static CaseOutcome Classify(
        ScriptCommand command,
        CommandObservation observation,
        bool fromCallback
    )
    {
        if (command is InvalidCommand || !observation.Diagnostics.IsEmpty)
        {
            return CaseOutcome.RunnerError;
        }
        if (observation.Exception is { } exception)
        {
            if (fromCallback)
            {
                return CaseOutcome.RunnerError;
            }
            if (
                exception
                is WasmUnsupportedFeatureException
                    or WasmImportInspectionException
                    {
                        Reason: WasmImportInspectionReason.UnsupportedFeature
                    }
            )
            {
                return CaseOutcome.RuntimeUnsupported;
            }
            if (
                exception
                is not (
                    WasmDecodeException
                    or WasmValidateException
                    or WasmInstantiateException
                    or WasmTrapException
                    or WasmExhaustionException
                )
            )
            {
                return CaseOutcome.RunnerError;
            }
            return
                MatchesFailure(command, observation.LastStage, exception)
                && exception.Message.StartsWith(ExpectedText(command)!, StringComparison.Ordinal)
                ? CaseOutcome.Passed
                : CaseOutcome.Failed;
        }
        if (!observation.BlockedBy.IsEmpty)
        {
            return CaseOutcome.Blocked;
        }
        if (
            command
            is ModuleCommand { ModuleType: ScriptModuleType.Text }
                or ModuleAssertionCommand { ModuleType: ScriptModuleType.Text }
        )
        {
            return CaseOutcome.OutOfScope;
        }

        var completed = command switch
        {
            ModuleCommand => observation.LastStage == CaseStage.Instantiate,
            RegisterCommand => observation.LastStage == CaseStage.Register,
            ActionCommand x => MatchesAction(x.Action, observation.LastStage),
            AssertReturnCommand x => MatchesAction(x.Action, observation.LastStage)
                && observation.Mismatches.IsEmpty,
            _ => false,
        };
        return completed ? CaseOutcome.Passed : CaseOutcome.Failed;
    }

    /// <summary>
    /// 否定assertionが要求する公開段階・例外型・リンク不成立のReasonを照合する。
    /// </summary>
    /// <param name="command">処理段階での失敗を期待する否定assertion</param>
    /// <param name="stage">例外が発生した公開段階 公開操作前はnull</param>
    /// <param name="exception">観測した例外の実体</param>
    /// <returns>期待する段階と失敗の種類が一致する場合はtrue 診断メッセージの一致は含まない</returns>
    private static bool MatchesFailure(ScriptCommand command, CaseStage? stage, Exception exception)
    {
        return command switch
        {
            AssertMalformedCommand { ModuleType: ScriptModuleType.Binary } => stage
                == CaseStage.Decode
                && exception is WasmDecodeException,
            AssertInvalidCommand { ModuleType: ScriptModuleType.Binary } => stage
                == CaseStage.Validate
                && exception is WasmValidateException,
            AssertUnlinkableCommand { ModuleType: ScriptModuleType.Binary } => stage
                == CaseStage.Instantiate
                && exception
                    is WasmInstantiateException
                    {
                        Reason: WasmInstantiateReason.MissingImport
                            or WasmInstantiateReason.KindMismatch
                            or WasmInstantiateReason.TypeMismatch
                    },
            AssertUninstantiableCommand { ModuleType: ScriptModuleType.Binary } => stage
                == CaseStage.Instantiate
                && exception is WasmTrapException,
            AssertTrapCommand x => MatchesAction(x.Action, stage) && exception is WasmTrapException,
            AssertExhaustionCommand x => MatchesAction(x.Action, stage)
                && exception is WasmExhaustionException,
            _ => false,
        };
    }

    /// <summary>
    /// actionの種類が要求する公開呼び出しを観測したか確認する。
    /// </summary>
    /// <param name="action">invokeまたはgetの操作</param>
    /// <param name="stage">最後に呼び出した公開段階 公開操作前はnull</param>
    /// <returns>invokeでInvoke、getでGetを観測した場合はtrue</returns>
    private static bool MatchesAction(ScriptAction action, CaseStage? stage)
    {
        return action switch
        {
            InvokeAction => stage == CaseStage.Invoke,
            GetAction => stage == CaseStage.Get,
            _ => false,
        };
    }

    /// <summary>
    /// 否定assertionのtextを加工せず取得する。
    /// </summary>
    /// <param name="command">期待診断の取得対象</param>
    /// <returns>否定assertionの期待診断 診断を期待しないcommandではnull</returns>
    private static string? ExpectedText(ScriptCommand command)
    {
        return command switch
        {
            ModuleAssertionCommand x => x.Text,
            AssertTrapCommand x => x.Text,
            AssertExhaustionCommand x => x.Text,
            _ => null,
        };
    }

    /// <summary>
    /// 値付き期待値または型だけの宣言を、JSONの記載順を保って保存用の記録へ写す。
    /// </summary>
    /// <param name="command">値付き期待値または型だけの結果宣言を持つcommand</param>
    /// <returns>新しい保存用リスト 値の文字列は加工せず、laneのリストもコピーし、宣言がなければ空</returns>
    private static List<ExpectedValueRecord> ExpectedValues(ScriptCommand command)
    {
        if (command is AssertReturnCommand assertion)
        {
            return
            [
                .. assertion.Expected.Select(x => new ExpectedValueRecord(
                    ValueCodec.GetTypeName(x.Kind)
                )
                {
                    Value = x.Value,
                    LaneType = x.LaneType?.ToString().ToLowerInvariant(),
                    Lanes = [.. x.Lanes],
                }),
            ];
        }
        var types = command switch
        {
            ActionCommand x => x.ResultTypes,
            AssertTrapCommand x => x.ResultTypes,
            AssertExhaustionCommand x => x.ResultTypes,
            _ => [],
        };
        return [.. types.Select(x => new ExpectedValueRecord(ValueCodec.GetTypeName(x)))];
    }

    /// <summary>
    /// 元診断の可変リストを共有せずに保存用のコピーを作る。
    /// </summary>
    /// <param name="diagnostic">観測に含まれる元診断</param>
    /// <returns>未確認範囲のリストを独立させた同じ内容の診断</returns>
    private static CaseDiagnostic CopyDiagnostic(CaseDiagnostic diagnostic)
    {
        return diagnostic with { UnverifiedRanges = [.. diagnostic.UnverifiedRanges] };
    }
}
