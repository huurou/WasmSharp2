namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// DecodeとValidateだけを行い、直近の定義と指定した定義名を更新するmodule定義
/// </summary>
/// <remarks>
/// リソースの割り当て・importのリンク・startの実行は行わず、直近moduleやmodule識別子は更新しない。
/// </remarks>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Name">定義名 省略時はnull</param>
/// <param name="Filename">JSONの親を基準にした素材のファイル名</param>
/// <param name="ModuleType">素材の形式 module_typeの省略時はbinary</param>
internal sealed record ModuleDefinitionCommand(
    int Index,
    int? Line,
    string? Name,
    string Filename,
    ScriptModuleType ModuleType
) : ScriptCommand(Index, Line)
{
    /// <summary>
    /// JSONのmodule定義を識別するcommand種別
    /// </summary>
    internal override string Type => MODULE_DEFINITION;
}
