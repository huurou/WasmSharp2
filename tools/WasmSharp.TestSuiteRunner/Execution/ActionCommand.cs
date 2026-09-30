using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assertionを伴わない単独action
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Action">実行するinvokeまたはget</param>
/// <param name="ResultTypes">型だけの結果宣言 追加のassertionとしては扱わない</param>
internal sealed record ActionCommand(
    int Index,
    int? Line,
    ScriptAction Action,
    ImmutableArray<WasmValueKind> ResultTypes
) : ScriptCommand(Index, Line)
{
    /// <summary>
    /// JSONの単独actionを識別するcommand種別
    /// </summary>
    internal override string Type => ACTION;
}
