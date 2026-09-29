namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// exportされたglobalの現在値を取得するget
/// </summary>
internal sealed record GetAction(string? Module, string Field) : ScriptAction(Module, Field);
