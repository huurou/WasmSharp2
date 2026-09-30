namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// exportされたglobalの現在値を取得するget
/// </summary>
/// <param name="Module">対象moduleの識別子 省略時は直近の通常moduleを示すnull</param>
/// <param name="Field">値を取得するglobalのexport名</param>
internal sealed record GetAction(string? Module, string Field) : ScriptAction(Module, Field);
