namespace WasmSharp.Exceptions;

/// <summary>
/// 未実装の機能に遭遇したことを示す例外
/// </summary>
public class WasmUnsupportedFeatureException : WasmException
{
    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmUnsupportedFeatureException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmUnsupportedFeatureException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmUnsupportedFeatureException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
