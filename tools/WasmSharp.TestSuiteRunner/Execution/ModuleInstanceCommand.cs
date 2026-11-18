namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 検証済みの定義をインスタンス化し、直近moduleと指定したmodule識別子を更新するcommand
/// </summary>
/// <remarks>
/// 定義のDecode・Validateは繰り返さない。成功するたびに、同じ定義から独立したinstanceを生成する。
/// </remarks>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Instance">生成したinstanceのmodule識別子 省略時はnull</param>
/// <param name="Module">インスタンス化する定義名 省略時は直近の定義を示すnull</param>
internal sealed record ModuleInstanceCommand(int Index, int? Line, string? Instance, string? Module)
    : ScriptCommand(Index, Line)
{
    /// <summary>
    /// JSONの定義のインスタンス化を識別するcommand種別
    /// </summary>
    internal override string Type => MODULE_INSTANCE;
}
