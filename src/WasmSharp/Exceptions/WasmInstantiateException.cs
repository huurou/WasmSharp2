namespace WasmSharp.Exceptions;

/// <summary>
/// Instantiate時例外 インスタンス化失敗・リンク失敗等
/// </summary>
public class WasmInstantiateException : WasmException
{
    public WasmInstantiateException() { }

    public WasmInstantiateException(string? message)
        : base(message) { }

    public WasmInstantiateException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
