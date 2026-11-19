using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assert_returnの1つの結果位置に指定された単独の期待値、または許容される値の候補
/// </summary>
internal abstract record ExpectedResult
{
    /// <summary>
    /// Core 3.0のeitherに記載された順序で、結果位置が許容する候補を保持する。
    /// </summary>
    internal sealed record Either(ImmutableArray<ExpectedValue> Candidates) : ExpectedResult;
}
