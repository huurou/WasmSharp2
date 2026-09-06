namespace WasmSharp.Exceptions;

/// <summary>
/// Wasmの実行規則で定められたtrapの原因
/// </summary>
public enum WasmTrapReason
{
    /// <summary>
    /// unreachable命令の実行
    /// </summary>
    Unreachable,

    /// <summary>
    /// 整数のゼロ除算
    /// </summary>
    IntegerDivideByZero,

    /// <summary>
    /// 符号付き整数除算のオーバーフロー
    /// </summary>
    IntegerOverflow,

    /// <summary>
    /// 整数への変換が不成立
    /// </summary>
    InvalidConversionToInteger,

    /// <summary>
    /// メモリの範囲外へのアクセス
    /// </summary>
    MemoryOutOfBounds,

    /// <summary>
    /// テーブルの範囲外へのアクセス
    /// </summary>
    TableOutOfBounds,

    /// <summary>
    /// 間接呼び出し先の関数型が不一致
    /// </summary>
    IndirectCallTypeMismatch,

    /// <summary>
    /// 間接呼び出し先の要素が未初期化
    /// </summary>
    UninitializedElement,
}
