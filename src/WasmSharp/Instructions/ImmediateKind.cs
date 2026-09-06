namespace WasmSharp.Instructions;

/// <summary>
/// 命令の即値の符号化
/// </summary>
public enum ImmediateKind
{
    /// <summary>
    /// 即値の読み取りが未対応
    /// </summary>
    Unsupported,

    /// <summary>
    /// 即値なし
    /// </summary>
    None,

    /// <summary>
    /// 符号付きLEB128で符号化された32ビット整数
    /// </summary>
    I32,

    /// <summary>
    /// 符号付きLEB128で符号化された64ビット整数
    /// </summary>
    I64,

    /// <summary>
    /// リトルエンディアンで符号化された32ビット浮動小数点数のビット列
    /// </summary>
    F32Bits,

    /// <summary>
    /// リトルエンディアンで符号化された64ビット浮動小数点数のビット列
    /// </summary>
    F64Bits,
}
