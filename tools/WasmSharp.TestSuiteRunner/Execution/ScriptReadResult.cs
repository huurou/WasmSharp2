using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 一つの入力の列挙済みcommandを順序どおりに読み取った結果
/// </summary>
/// <param name="InputPath">test/core基準の/区切り相対path</param>
/// <param name="Commands">列挙済みcommandのindex順の読取結果 読み取れないcommandは同じ位置のInvalidCommandとする</param>
/// <param name="EnumerationComplete">JSONの全commandを列挙できたかどうか falseの場合、総数は未確定</param>
internal sealed record ScriptReadResult(
    string InputPath,
    ImmutableArray<ScriptCommand> Commands,
    bool EnumerationComplete
);
