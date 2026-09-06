namespace WasmSharp.Exceptions;

/// <summary>
/// 未サポート機能例外 仕様上有効だが未実装
/// </summary>
public class WasmUnsupportedFeatureException : WasmException
{
    public WasmUnsupportedFeatureException() { }

    public WasmUnsupportedFeatureException(string? message)
        : base(message) { }

    public WasmUnsupportedFeatureException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
