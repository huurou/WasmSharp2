using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assert_returnの値または参照条件を表すスクリプト上の期待値
/// </summary>
/// <remarks>
/// 具体値とNaN patternの文字列を加工せず保持し、解釈はactionの前の比較用の解析で行う。型だけの結果宣言とは型を分ける。
/// </remarks>
/// <param name="Type">変換器が出力した値型名</param>
/// <param name="Value">scalarと参照の値またはNaN patternの文字列 v128と値を省略した参照期待値ではnull</param>
/// <param name="LaneType">v128のlane型 v128以外ではnull</param>
/// <param name="Lanes">v128のlane0から順の値またはNaN patternの文字列 v128以外では空</param>
internal sealed record ExpectedValue(string Type, string? Value, LaneType? LaneType, ImmutableArray<string> Lanes)
    : ExpectedResult
{
    /// <summary>
    /// Core 3.0の参照型で値を省略した場合、nullを明示した場合、番号を指定した場合の比較条件を返す。
    /// </summary>
    /// <returns>参照型でなければnull 固定形式にない表記は対応する比較条件がnullになる。</returns>
    internal static (
        Core3ReferenceMatch? Omitted,
        Core3ReferenceMatch? ExplicitNull,
        Core3ReferenceMatch? Numbered
    )? GetCore3ReferenceMatches(string type)
    {
        return type switch
        {
            "refnull" or "nullref" or "nullfuncref" or "nullexternref" or "nullexnref" => (
                Core3ReferenceMatch.Null,
                null,
                null
            ),
            "funcref" => (Core3ReferenceMatch.HeapType, Core3ReferenceMatch.Null, null),
            "externref" or "anyref" => (
                Core3ReferenceMatch.HeapType,
                Core3ReferenceMatch.Null,
                Core3ReferenceMatch.Identity
            ),
            "exnref" => (null, Core3ReferenceMatch.Null, null),
            "eqref" or "i31ref" or "arrayref" or "structref" => (Core3ReferenceMatch.HeapType, null, null),
            _ => null,
        };
    }
}
