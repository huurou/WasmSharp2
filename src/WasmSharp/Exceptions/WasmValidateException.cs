namespace WasmSharp.Exceptions;

/// <summary>
/// 型規則や構造規則への違反などによりValidateに失敗したことを示す例外
/// </summary>
public class WasmValidateException : WasmException
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmValidateException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmValidateException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmValidateException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
