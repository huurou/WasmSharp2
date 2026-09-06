namespace WasmSharp.Instructions;

/// <summary>
/// 命令のスタック効果
/// </summary>
public enum StackEffectKind
{
    /// <summary>
    /// スタック効果の処理が未対応
    /// </summary>
    Unsupported,

    /// <summary>
    /// 32ビット整数をスタックに積む
    /// </summary>
    PushI32,

    /// <summary>
    /// 64ビット整数をスタックに積む
    /// </summary>
    PushI64,

    /// <summary>
    /// 32ビット浮動小数点数をスタックに積む
    /// </summary>
    PushF32,

    /// <summary>
    /// 64ビット浮動小数点数をスタックに積む
    /// </summary>
    PushF64,

    /// <summary>
    /// 関数の終端で戻り値を確定する
    /// </summary>
    FunctionEnd,
}
