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

    /// <summary>
    /// 即値の参照型を持つnullを積む
    /// </summary>
    PushReference,

    /// <summary>
    /// 参照型の値を取り除き、null判定の結果をi32で積む
    /// </summary>
    RefIsNull,

    /// <summary>
    /// 指定関数のfuncrefを積む
    /// </summary>
    RefFunc,

    /// <summary>
    /// i32の要素添字を取り除き、テーブルの参照型の値を積む
    /// </summary>
    TableGet,

    /// <summary>
    /// テーブルの参照型の値とi32の要素添字を取り除く
    /// </summary>
    TableSet,

    /// <summary>
    /// 要素初期化の長さ、コピー元とコピー先を表す3つのi32値を取り除く
    /// </summary>
    TableInit,

    /// <summary>
    /// 要素セグメントを破棄し、スタックを変更しない
    /// </summary>
    ElementDrop,

    /// <summary>
    /// テーブルのコピーの長さ、コピー元とコピー先を表す3つのi32値を取り除く
    /// </summary>
    TableCopy,

    /// <summary>
    /// i32の増分と参照型の初期値を取り除き、増大前の要素数をi32で積む
    /// </summary>
    TableGrow,

    /// <summary>
    /// テーブルの要素数をi32で積む
    /// </summary>
    TableSize,

    /// <summary>
    /// i32の長さ、参照型の値、i32の開始位置を取り除く
    /// </summary>
    TableFill,

    /// <summary>
    /// i32の選択値と指定型の引数を取り除き、その結果を積む
    /// </summary>
    CallIndirect,

    /// <summary>
    /// 128ビットvectorをスタックに積む
    /// </summary>
    PushV128,

    /// <summary>
    /// v128とi32アドレスを取り除き、laneを置き換えたv128を積む
    /// </summary>
    MemoryLoadLane,

    /// <summary>
    /// v128とi32アドレスを取り除き、指定laneを書き込む
    /// </summary>
    MemoryStoreLane,
}
