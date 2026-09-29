using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// actionの実行でexhaustionを期待するassert_exhaustion
/// </summary>
/// <param name="Action">実行するinvokeまたはget</param>
/// <param name="Text">加工しない期待診断</param>
/// <param name="ResultTypes">型だけの結果宣言</param>
internal sealed record AssertExhaustionCommand(
    int Index,
    int? Line,
    ScriptAction Action,
    string Text,
    ImmutableArray<WasmValueKind> ResultTypes
) : ScriptCommand(Index, Line)
{
    internal override string Type => ASSERT_EXHAUSTION;
}
