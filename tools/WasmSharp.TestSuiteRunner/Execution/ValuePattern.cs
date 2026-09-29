using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 文字列を検査して比較用に解析した、assert_returnの1個の期待値
/// </summary>
/// <param name="Kind">値型</param>
/// <param name="Width">Bitsの1個あたりのビット幅 数値では型の幅、v128ではlaneの幅、参照では0</param>
/// <param name="Bits">数値では1個、v128ではlane0から順のlane数の期待 参照では空</param>
/// <param name="Externref">externrefの非null期待値の番号 それ以外の期待値ではnull</param>
internal sealed record ValuePattern(
    WasmValueKind Kind,
    int Width,
    ImmutableArray<BitsPattern> Bits,
    uint? Externref
);
