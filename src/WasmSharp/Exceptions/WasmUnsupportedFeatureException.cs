namespace WasmSharp.Exceptions;

/// <summary>
/// 未実装の機能に遭遇したことを示す例外
/// </summary>
public class WasmUnsupportedFeatureException : WasmException
{
    public WasmUnsupportedFeatureException() { }

    public WasmUnsupportedFeatureException(string? message)
        : base(message) { }

    public WasmUnsupportedFeatureException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
