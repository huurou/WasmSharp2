using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assert_returnの値付き期待値のWABT表現
/// </summary>
/// <remarks>
/// 具体値とNaN patternの文字列を加工せず保持し、解釈はactionの前の比較用の解析で行う。型だけの結果宣言とは型を分ける。
/// </remarks>
/// <param name="Kind">値型</param>
/// <param name="Value">scalarと参照の値またはNaN patternの文字列。v128ではnull</param>
/// <param name="LaneType">v128のlane型。v128以外ではnull</param>
/// <param name="Lanes">v128のlane0から順の値またはNaN patternの文字列。v128以外では空</param>
internal sealed record ExpectedValue(
    WasmValueKind Kind,
    string? Value,
    LaneType? LaneType,
    ImmutableArray<string> Lanes
);
