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

    /// <summary>
    /// 型スタックを変更しない
    /// </summary>
    None,

    /// <summary>
    /// 数値signatureの入力を取り除き結果を1個積む
    /// </summary>
    Numeric,

    /// <summary>
    /// block型の引数と結果を持つ制御frameを開始する
    /// </summary>
    Block,

    /// <summary>
    /// loop型の引数と結果を持つ制御frameを開始する
    /// </summary>
    Loop,

    /// <summary>
    /// i32条件を取り除き、if型の制御frameを開始する
    /// </summary>
    If,

    /// <summary>
    /// thenの結果を確定し、elseの入力へ戻す
    /// </summary>
    Else,

    /// <summary>
    /// 現在の制御frameを終了し、その結果を外側へ積む
    /// </summary>
    ControlEnd,

    /// <summary>
    /// ラベルの値を取り出して到達不能にする
    /// </summary>
    Branch,

    /// <summary>
    /// i32条件を取り除き、非分岐経路にラベルの値を残す
    /// </summary>
    BranchIf,

    /// <summary>
    /// i32選択値とラベルの値を取り出して到達不能にする
    /// </summary>
    BranchTable,

    /// <summary>
    /// i32条件と同じ数値型の2候補を取り除き、候補型の値を積む
    /// </summary>
    Select,

    /// <summary>
    /// i32条件と注釈型の2候補を取り除き、注釈型の値を積む
    /// </summary>
    TypedSelect,

    /// <summary>
    /// i32アドレスを取り除き、メモリアクセス情報の値型を積む
    /// </summary>
    MemoryLoad,

    /// <summary>
    /// メモリアクセス情報の値型とi32アドレスを取り除く
    /// </summary>
    MemoryStore,

    /// <summary>
    /// i32のページ数を取り除き、拡張前のページ数をi32で積む
    /// </summary>
    MemoryGrow,

    /// <summary>
    /// 3つのi32値を取り除く
    /// </summary>
    MemoryBulk,
}
