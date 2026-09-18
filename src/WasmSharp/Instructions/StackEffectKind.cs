namespace WasmSharp.Instructions;

/// <summary>
/// 命令のスタック効果
/// </summary>
internal enum StackEffectKind
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

    /// <summary>
    /// 実行を中断し、型スタックを到達不能にする
    /// </summary>
    Unreachable,

    /// <summary>
    /// 関数型に従って引数を取り除き結果を積む
    /// </summary>
    Call,

    /// <summary>
    /// 関数の宣言結果を取り出して到達不能にする
    /// </summary>
    Return,

    /// <summary>
    /// 任意の型の値を1個取り除く
    /// </summary>
    Drop,

    /// <summary>
    /// 指定localの型の値を積む
    /// </summary>
    LocalGet,

    /// <summary>
    /// 指定localの型の値を取り除く
    /// </summary>
    LocalSet,

    /// <summary>
    /// 指定localの型の値を確認しスタックに残す
    /// </summary>
    LocalTee,

    /// <summary>
    /// 指定globalの型の値を積む
    /// </summary>
    GlobalGet,

    /// <summary>
    /// 指定したmutable globalの型の値を取り除く
    /// </summary>
    GlobalSet,
}
