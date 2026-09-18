namespace WasmSharp.Instructions;

/// <summary>
/// 命令に適用する検証規則
/// </summary>
internal enum ValidationRule
{
    /// <summary>
    /// 命令の検証が未対応
    /// </summary>
    Unsupported,

    /// <summary>
    /// 定数の型を型スタックに積む
    /// </summary>
    Constant,

    /// <summary>
    /// 型スタックの値の型と個数が関数の戻り値型と一致することを検証する
    /// </summary>
    FunctionEnd,

    /// <summary>
    /// 型スタックを関数底へ戻し、到達不能にする
    /// </summary>
    Unreachable,

    /// <summary>
    /// 関数添字と引数型を検証し、結果型を型スタックに積む
    /// </summary>
    Call,

    /// <summary>
    /// 関数の宣言結果の型を検証し、到達不能にする
    /// </summary>
    Return,

    /// <summary>
    /// 型スタックから任意の型を1個取り除く
    /// </summary>
    Drop,

    /// <summary>
    /// local添字を検証し、その型を型スタックに積む
    /// </summary>
    LocalGet,

    /// <summary>
    /// local添字と入力型を検証し、入力型を型スタックから取り除く
    /// </summary>
    LocalSet,

    /// <summary>
    /// local添字と入力型を検証し、入力型を型スタックに残す
    /// </summary>
    LocalTee,

    /// <summary>
    /// global添字を検証し、その型を型スタックに積む
    /// </summary>
    GlobalGet,

    /// <summary>
    /// global添字・可変性・入力型を検証し、入力型を型スタックから取り除く
    /// </summary>
    GlobalSet,
}
