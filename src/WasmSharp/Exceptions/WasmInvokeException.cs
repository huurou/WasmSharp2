namespace WasmSharp.Exceptions;

/// <summary>
/// Invoke時例外
/// </summary>
public class WasmInvokeException : WasmException
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmInvokeException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmInvokeException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmInvokeException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
