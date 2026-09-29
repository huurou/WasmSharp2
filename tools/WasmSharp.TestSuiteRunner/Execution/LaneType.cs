namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// v128の値を分割するlane型
/// </summary>
internal enum LaneType
{
    /// <summary>
    /// 8ビット整数の16lane
    /// </summary>
    I8,

    /// <summary>
    /// 16ビット整数の8lane
    /// </summary>
    I16,

    /// <summary>
    /// 32ビット整数の4lane
    /// </summary>
    I32,

    /// <summary>
    /// 64ビット整数の2lane
    /// </summary>
    I64,

    /// <summary>
    /// 32ビット浮動小数点数の4lane
    /// </summary>
    F32,

    /// <summary>
    /// 64ビット浮動小数点数の2lane
    /// </summary>
    F64,
}
