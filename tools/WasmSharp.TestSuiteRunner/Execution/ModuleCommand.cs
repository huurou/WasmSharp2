namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 直近moduleと指定識別子を更新する通常module
/// </summary>
/// <param name="Name">module識別子。省略時はnull</param>
/// <param name="Filename">JSONの親を基準にした素材のファイル名</param>
/// <param name="ModuleType">素材の形式。module_typeの省略時はbinary</param>
internal sealed record ModuleCommand(
    int Index,
    int? Line,
    string? Name,
    string Filename,
    ScriptModuleType ModuleType
) : ScriptCommand(Index, Line)
{
    internal override string Type => MODULE;
}
