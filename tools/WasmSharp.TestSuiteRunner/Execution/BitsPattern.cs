namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 数値またはv128の1laneに対する、具体的なビット列かNaN patternの期待
/// </summary>
/// <param name="Bits">具体値のビット列 NaN patternでは0</param>
/// <param name="Nan">NaN pattern 具体値ではnull</param>
internal sealed record BitsPattern(ulong Bits, NanPattern? Nan);
