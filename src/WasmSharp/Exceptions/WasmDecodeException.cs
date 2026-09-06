namespace WasmSharp.Exceptions;

/// <summary>
/// バイナリの破損などによりDecodeに失敗したことを示す例外
/// </summary>
public class WasmDecodeException : WasmException
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmDecodeException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmDecodeException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmDecodeException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// メッセージ、発生位置、原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="location">失敗した処理段階と入力上の位置</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmDecodeException(
        string? message,
        WasmFailureLocation location,
        Exception? innerException
    )
        : base(message, location, innerException) { }
}
