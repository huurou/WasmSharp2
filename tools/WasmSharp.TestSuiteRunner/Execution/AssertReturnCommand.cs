using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// actionの正常完了と結果の一致を期待するassert_return
/// </summary>
/// <param name="Action">実行するinvokeまたはget</param>
/// <param name="Expected">結果の順序どおりの値付き期待値</param>
internal sealed record AssertReturnCommand(
    int Index,
    int? Line,
    ScriptAction Action,
    ImmutableArray<ExpectedValue> Expected
) : ScriptCommand(Index, Line)
{
    internal override string Type => ASSERT_RETURN;
}
