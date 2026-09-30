using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// exportされた関数を順序付きの引数で呼び出すinvoke
/// </summary>
/// <param name="Module">対象moduleの識別子 省略時は直近の通常moduleを示すnull</param>
/// <param name="Field">呼び出す関数のexport名</param>
/// <param name="Arguments">JSONの記載順の引数</param>
internal sealed record InvokeAction(
    string? Module,
    string Field,
    ImmutableArray<ArgumentValue> Arguments
) : ScriptAction(Module, Field);
