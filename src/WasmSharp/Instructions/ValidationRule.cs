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

    /// <summary>
    /// 型スタックを変更しない
    /// </summary>
    None,

    /// <summary>
    /// 数値signatureに従って入力の個数と型を検証し、結果型を積む
    /// </summary>
    Numeric,

    /// <summary>
    /// block型と入力を検証し、結果をラベル型とする制御frameを開始する
    /// </summary>
    Block,

    /// <summary>
    /// loop型と入力を検証し、引数をラベル型とする制御frameを開始する
    /// </summary>
    Loop,

    /// <summary>
    /// i32条件とif型の入力を検証し、制御frameを開始する
    /// </summary>
    If,

    /// <summary>
    /// thenの結果を検証し、elseの入力へ戻す
    /// </summary>
    Else,

    /// <summary>
    /// 現在の制御frameの結果を検証し、外側へ積む
    /// </summary>
    ControlEnd,

    /// <summary>
    /// ラベル添字と値の型を検証し、到達不能にする
    /// </summary>
    Branch,

    /// <summary>
    /// i32条件とラベル型を検証し、非分岐経路の値を維持する
    /// </summary>
    BranchIf,

    /// <summary>
    /// i32選択値と全候補の共通ラベル型を検証し、到達不能にする
    /// </summary>
    BranchTable,

    /// <summary>
    /// i32条件と2候補の同じスカラー数値型を検証する
    /// </summary>
    Select,

    /// <summary>
    /// 型注釈が1個で、i32条件と2候補が注釈型に合うことを検証する
    /// </summary>
    TypedSelect,

    /// <summary>
    /// メモリとアラインメント、i32アドレスを検証し、アクセス情報の値型を積む
    /// </summary>
    MemoryLoad,

    /// <summary>
    /// メモリとアラインメント、書き込む値の型とi32アドレスを検証する
    /// </summary>
    MemoryStore,

    /// <summary>
    /// メモリが存在することを検証し、ページ数をi32で積む
    /// </summary>
    MemorySize,

    /// <summary>
    /// メモリとi32のページ数を検証し、拡張前のページ数をi32で積む
    /// </summary>
    MemoryGrow,

    /// <summary>
    /// メモリが存在し、3つの入力がi32型であることを検証する
    /// </summary>
    MemoryBulk,

    /// <summary>
    /// メモリとデータインデックス、3つのi32入力を検証する
    /// </summary>
    MemoryInit,

    /// <summary>
    /// データインデックスを検証し、型スタックを変更しない
    /// </summary>
    DataDrop,

    /// <summary>
    /// 参照型の入力を検証し、null判定の結果型i32を積む
    /// </summary>
    RefIsNull,

    /// <summary>
    /// 関数添字と参照の宣言を検証し、funcref型を積む
    /// </summary>
    RefFunc,

    /// <summary>
    /// テーブル添字とi32の要素添字を検証し、要素の参照型を積む
    /// </summary>
    TableGet,

    /// <summary>
    /// テーブル添字とi32の要素添字、要素の参照型を検証する
    /// </summary>
    TableSet,

    /// <summary>
    /// テーブルと要素セグメントの添字と参照型、3つのi32入力を検証する
    /// </summary>
    TableInit,

    /// <summary>
    /// 要素セグメント添字を検証し、型スタックを変更しない
    /// </summary>
    ElementDrop,

    /// <summary>
    /// コピー先とコピー元のテーブル添字と参照型、3つのi32入力を検証する
    /// </summary>
    TableCopy,

    /// <summary>
    /// テーブル添字と参照型の初期値、i32の増分を検証し、結果型i32を積む
    /// </summary>
    TableGrow,

    /// <summary>
    /// テーブル添字を検証し、要素数の型i32を積む
    /// </summary>
    TableSize,

    /// <summary>
    /// テーブル添字とi32の開始位置、要素の参照型、i32の長さを検証する
    /// </summary>
    TableFill,

    /// <summary>
    /// funcrefテーブルと型添字、i32の選択値、引数型を検証し、結果型を積む
    /// </summary>
    CallIndirect,
}
