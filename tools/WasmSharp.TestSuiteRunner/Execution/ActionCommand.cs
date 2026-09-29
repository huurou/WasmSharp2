using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assertionを伴わない単独action
/// </summary>
/// <param name="Action">実行するinvokeまたはget</param>
/// <param name="ResultTypes">型だけの結果宣言。追加のassertionとしては扱わない</param>
internal sealed record ActionCommand(
    int Index,
    int? Line,
    ScriptAction Action,
    ImmutableArray<WasmValueKind> ResultTypes
) : ScriptCommand(Index, Line)
{
    internal override string Type => ACTION;
}
