using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// invokeへ渡す引数のWABT表現
/// </summary>
/// <remarks>
/// 値の文字列は加工せず保持し、ビット列や参照への変換は引数構築時に行う。期待値だけが持つNaN patternとは型を分ける。
/// </remarks>
/// <param name="Kind">値型</param>
/// <param name="Value">scalarと参照の値の文字列。v128ではnull</param>
/// <param name="LaneType">v128のlane型。v128以外ではnull</param>
/// <param name="Lanes">v128のlane0から順の値の文字列。v128以外では空</param>
internal sealed record ArgumentValue(
    WasmValueKind Kind,
    string? Value,
    LaneType? LaneType,
    ImmutableArray<string> Lanes
);
