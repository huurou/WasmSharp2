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
    /// ラベル型と参照入力を検証し、分岐しない経路には非null参照を残す
    /// </summary>
    BranchOnNull,

    /// <summary>
    /// 絞り込んだ参照をラベルの末尾の型と照合し、分岐しない経路からは取り除く
    /// </summary>
    BranchOnNonNull,

    /// <summary>
    /// キャストが成功したら対象型をラベルに渡し、失敗したら元の型の参照を残す
    /// </summary>
    BranchOnCast,

    /// <summary>
    /// キャストが失敗したら元の型の参照をラベルに渡し、成功したら対象型の参照を残す
    /// </summary>
    BranchOnCastFail,

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
    /// 対象メモリのアドレス型と即値のオフセット、アラインメントを検証し、読み出す値の型を積む
    /// </summary>
    MemoryLoad,

    /// <summary>
    /// 対象メモリのアドレス型と即値のオフセット、アラインメント、書き込む値の型を検証する
    /// </summary>
    MemoryStore,

    /// <summary>
    /// メモリが存在することを検証し、ページ数をそのアドレス型で積む
    /// </summary>
    MemorySize,

    /// <summary>
    /// 指定したメモリのアドレス型と増分の型が一致することを検証し、同じ型で拡張前のページ数を積む
    /// </summary>
    MemoryGrow,

    /// <summary>
    /// コピー先とコピー元は各メモリのアドレス型、長さは幅が小さい方のアドレス型で検証する
    /// </summary>
    MemoryCopy,

    /// <summary>
    /// 書き込み先と長さは対象メモリのアドレス型、充填値はi32で検証する
    /// </summary>
    MemoryFill,

    /// <summary>
    /// メモリとデータのインデックスを確認し、宛先はメモリのアドレス型、データの開始位置と長さはi32で検証する
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
    /// 2個のeq参照を取り除き、同一性の判定結果をi32で積む
    /// </summary>
    RefEq,

    /// <summary>
    /// 対象型と同じ参照階層の値を取り除き、型判定の結果をi32で積む
    /// </summary>
    RefTest,

    /// <summary>
    /// 対象型と同じ参照階層の値を取り除き、対象型の参照として積む
    /// </summary>
    RefCast,

    /// <summary>
    /// 定数式と共有する単項演算の入力型を検証し、入力のnull可否を引き継いだ参照型を積む
    /// </summary>
    ConstantReference,

    /// <summary>
    /// nullを許可するi31参照を取り除き、i32を積む
    /// </summary>
    I31Get,

    /// <summary>
    /// 宣言順のフィールド値を取り除き、非nullの構造体参照を積む
    /// </summary>
    StructNew,

    /// <summary>
    /// フィールドを既定値で初期化した非nullの構造体参照を積む
    /// </summary>
    StructNewDefault,

    /// <summary>
    /// 構造体参照を取り除き、指定したフィールドの値を積む
    /// </summary>
    StructGet,

    /// <summary>
    /// 詰め込みフィールドの構造体参照を取り除き、i32を積む
    /// </summary>
    StructGetPacked,

    /// <summary>
    /// フィールドの値と書き込み先の構造体参照を取り除く
    /// </summary>
    StructSet,

    /// <summary>
    /// 参照型の入力を検証し、同じヒープ型の非null参照を積む
    /// </summary>
    RefAsNonNull,

    /// <summary>
    /// 関数添字と参照の宣言を検証し、funcref型を積む
    /// </summary>
    RefFunc,

    /// <summary>
    /// 指定テーブルのアドレス型で要素添字を検証し、要素の参照型を積む
    /// </summary>
    TableGet,

    /// <summary>
    /// 指定テーブルのアドレス型で要素添字を検証し、書き込む要素の参照型を検証する
    /// </summary>
    TableSet,

    /// <summary>
    /// 指定したテーブルと要素セグメントが存在し、要素型が一致することを検証する。
    /// コピー先の位置はテーブルのアドレス型、セグメント内の開始位置と長さはi32で検証する
    /// </summary>
    TableInit,

    /// <summary>
    /// 要素セグメント添字を検証し、型スタックを変更しない
    /// </summary>
    ElementDrop,

    /// <summary>
    /// コピー先とコピー元のテーブルが存在し、要素型が一致することを検証する。
    /// 開始位置は各テーブルのアドレス型、長さは両者のうち幅が小さい方のアドレス型で検証する
    /// </summary>
    TableCopy,

    /// <summary>
    /// テーブル添字と参照型の初期値、アドレス型の増分を検証し、同じアドレス型を積む
    /// </summary>
    TableGrow,

    /// <summary>
    /// テーブル添字を検証し、テーブルのアドレス型を積む
    /// </summary>
    TableSize,

    /// <summary>
    /// テーブルの添字と要素の参照型を検証し、開始位置と長さをテーブルのアドレス型で検証する
    /// </summary>
    TableFill,

    /// <summary>
    /// funcrefテーブルと型添字を検証し、要素添字をテーブルのアドレス型で検証する。
    /// 指定した関数型の引数を検証し、結果型を積む
    /// </summary>
    CallIndirect,

    /// <summary>
    /// 型添字が指す関数型に参照と引数が適合することを検証し、結果型を積む
    /// </summary>
    CallRef,

    /// <summary>
    /// メモリのロードと同様にアドレスと即値を検証し、lane番号とv128の入力を確認してv128を積む
    /// </summary>
    MemoryLoadLane,

    /// <summary>
    /// メモリのストアと同様にアドレスと即値、v128の入力を検証し、lane番号を確認する
    /// </summary>
    MemoryStoreLane,
}
