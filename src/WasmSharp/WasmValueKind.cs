namespace WasmSharp;

/// <summary>
/// valueの種類
/// </summary>
public enum WasmValueKind
{
    /// <summary>
    /// 32ビット整数
    /// </summary>
    I32,

    /// <summary>
    /// 64ビット整数
    /// </summary>
    I64,

    /// <summary>
    /// 32ビット浮動小数点数
    /// </summary>
    F32,

    /// <summary>
    /// 64ビット浮動小数点数
    /// </summary>
    F64,

    /// <summary>
    /// 128ビットベクトル
    /// </summary>
    V128,

    /// <summary>
    /// 関数への参照
    /// </summary>
    FuncRef,

    /// <summary>
    /// ホスト側のvalueへの外部参照
    /// </summary>
    ExternRef,

    /// <summary>
    /// Wasm例外への参照
    /// </summary>
    ExnRef,
}
