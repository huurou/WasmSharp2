namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 単独actionとassertionが対象moduleのexportへ行う操作
/// </summary>
/// <param name="Module">対象moduleの識別子 省略時は直近の通常moduleを示すnull</param>
/// <param name="Field">対象のexport名</param>
internal abstract record ScriptAction(string? Module, string Field);
