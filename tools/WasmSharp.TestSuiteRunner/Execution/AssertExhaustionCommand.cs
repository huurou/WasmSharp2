using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// actionの実行でexhaustionを期待するassert_exhaustion
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Action">実行するinvokeまたはget</param>
/// <param name="Text">加工しない期待診断</param>
/// <param name="ResultTypes">型だけの結果宣言 exhaustionの成立条件へ追加しない</param>
internal sealed record AssertExhaustionCommand(
    int Index,
    int? Line,
    ScriptAction Action,
    string Text,
    ImmutableArray<WasmValueKind> ResultTypes
) : ScriptCommand(Index, Line)
{
    /// <summary>
    /// JSONのexhaustionを期待するassertionを識別するcommand種別
    /// </summary>
    internal override string Type => ASSERT_EXHAUSTION;
}
