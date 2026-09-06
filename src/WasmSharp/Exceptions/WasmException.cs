namespace WasmSharp.Exceptions;

/// <summary>
/// WasmSharp例外基底クラス
/// </summary>
public abstract class WasmException : Exception
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
