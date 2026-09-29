namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// moduleの全exportを登録名に対応付けるregister
/// </summary>
/// <param name="Name">登録するmoduleの識別子。省略時は直近の通常moduleを示すnull</param>
/// <param name="As">importで使う登録名</param>
internal sealed record RegisterCommand(int Index, int? Line, string? Name, string As)
    : ScriptCommand(Index, Line)
{
    internal override string Type => REGISTER;
}
