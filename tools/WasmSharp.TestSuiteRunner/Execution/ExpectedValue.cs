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
{
    internal ExpectedValue(WasmValueKind kind, string? value, LaneType? laneType, ImmutableArray<string> lanes)
        : this(ValueCodec.GetTypeName(kind), value, laneType, lanes) { }

    /// <summary>
    /// 固定したCore 3.0の参照期待値に使う型名か判定する。
    /// </summary>
    internal static bool IsCore3ReferenceType(string type)
    {
        return type
            is "refnull"
                or "nullref"
                or "nullfuncref"
                or "nullexternref"
                or "nullexnref"
                or "funcref"
                or "externref"
                or "anyref"
                or "exnref"
                or "eqref"
                or "i31ref"
                or "arrayref"
                or "structref";
    }
}
