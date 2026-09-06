namespace WasmSharp.Exceptions;

/// <summary>
/// CPU機能などが利用できないプラットフォーム側の問題を示す例外
/// </summary>
public class WasmPlatformCapabilityException : WasmException
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmPlatformCapabilityException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmPlatformCapabilityException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmPlatformCapabilityException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
