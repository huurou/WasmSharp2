namespace WasmSharp.Exceptions;

/// <summary>
/// WasmSharp例外基底クラス
/// </summary>
public abstract class WasmException : Exception
{
    public WasmException() { }

    public WasmException(string? message)
        : base(message) { }

    public WasmException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
