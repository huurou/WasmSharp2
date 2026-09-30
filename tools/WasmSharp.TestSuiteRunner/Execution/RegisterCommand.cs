namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// moduleの全exportを登録名に対応付けるregister
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Name">登録するmoduleの識別子 省略時は直近の通常moduleを示すnull</param>
/// <param name="As">importで使う登録名</param>
internal sealed record RegisterCommand(int Index, int? Line, string? Name, string As)
    : ScriptCommand(Index, Line)
{
    /// <summary>
    /// JSONの登録名への対応付けを識別するcommand種別
    /// </summary>
    internal override string Type => REGISTER;
}
