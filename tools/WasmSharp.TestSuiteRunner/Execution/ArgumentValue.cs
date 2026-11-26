using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// invokeへ渡す引数のスクリプト上の表現
/// </summary>
/// <remarks>
/// 値の文字列は加工せず保持し、ビット列や参照への変換は引数構築時に行う。期待値だけが持つNaN patternとは型を分ける。
/// </remarks>
/// <param name="Type">変換器が出力した値型名</param>
/// <param name="Value">scalarと参照の値の文字列 v128と値を省略した参照引数ではnull</param>
/// <param name="LaneType">v128のlane型 v128以外ではnull</param>
/// <param name="Lanes">v128のlane0から順の値の文字列 v128以外では空</param>
internal sealed record ArgumentValue(string Type, string? Value, LaneType? LaneType, ImmutableArray<string> Lanes)
{
    /// <summary>
    /// Core 3.0の引数で値を省略する参照型名から、nullが属する参照階層を取得する
    /// </summary>
    internal static WasmValueKind? GetCore3NullReferenceKind(string type)
    {
        return type switch
        {
            "nullref" or "eqref" or "i31ref" or "structref" or "arrayref" => WasmValueKind.AnyRef,
            "nullfuncref" => WasmValueKind.FuncRef,
            "nullexternref" => WasmValueKind.ExternRef,
            _ => null,
        };
    }
}
