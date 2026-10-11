namespace WasmSharp.Instructions;

/// <summary>
/// 命令の即値の符号化
/// </summary>
internal enum ImmediateKind
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

    /// <summary>
    /// 符号なしLEB128で符号化された32ビット添字
    /// </summary>
    Index,

    /// <summary>
    /// 空型、単一の値型またはs33の型添字で符号化された制御型
    /// </summary>
    BlockType,

    /// <summary>
    /// 候補ラベルのベクトルと既定ラベル
    /// </summary>
    BranchTable,

    /// <summary>
    /// selectの型注釈を表す値型のベクトル
    /// </summary>
    TypeVector,
}
