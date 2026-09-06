namespace WasmSharp.Exceptions;

/// <summary>
/// インスタンス化やリンクの失敗などによりInstantiateに失敗したことを示す例外
/// </summary>
public class WasmInstantiateException : WasmException
{
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
}
