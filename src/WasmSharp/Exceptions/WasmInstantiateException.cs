namespace WasmSharp.Exceptions;

/// <summary>
/// インスタンス化やリンクの失敗などによりInstantiateに失敗したことを示す例外
/// </summary>
public class WasmInstantiateException : WasmException
{
    /// <summary>
    /// リンク不成立の理由。従来のコンストラクターではnull
    /// </summary>
    public WasmInstantiateReason? Reason { get; }

    /// <summary>
    /// 不成立となったimportの宣言番号（0始まり）
    /// </summary>
    public int? ImportOrdinal { get; }

    /// <summary>
    /// importが要求したmodule名
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>
    /// importが要求したitem名
    /// </summary>
    public string? ImportName { get; }

    /// <summary>
    /// importが要求した外部要素の種類
    /// </summary>
    public WasmExternalKind? ExpectedKind { get; }

    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmInstantiateException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmInstantiateException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmInstantiateException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// 不成立となったimportの識別情報と位置を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="reason">リンク不成立の理由</param>
    /// <param name="importOrdinal">不成立となったimportの宣言番号（0始まり）</param>
    /// <param name="moduleName">importが要求したmodule名</param>
    /// <param name="importName">importが要求したitem名</param>
    /// <param name="expectedKind">importが要求した外部要素の種類</param>
    /// <param name="location">不成立となったimportの宣言位置</param>
    public WasmInstantiateException(
        string? message,
        WasmInstantiateReason reason,
        int importOrdinal,
        string moduleName,
        string importName,
        WasmExternalKind expectedKind,
        WasmFailureLocation location
    )
        : base(message, location, null)
    {
        Reason = reason;
        ImportOrdinal = importOrdinal;
        ModuleName = moduleName;
        ImportName = importName;
        ExpectedKind = expectedKind;
    }
}

/// <summary>
/// importを接続できない理由
/// </summary>
public enum WasmInstantiateReason
{
    /// <summary>
    /// 名前が一致する提供登録がない
    /// </summary>
    MissingImport,

    /// <summary>
    /// 外部要素の種類が異なる
    /// </summary>
    KindMismatch,

    /// <summary>
    /// 要求型に適合しない
    /// </summary>
    TypeMismatch,
}
