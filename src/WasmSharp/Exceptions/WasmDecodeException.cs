namespace WasmSharp.Exceptions;

/// <summary>
/// Decode時例外 バイナリが壊れている等
/// </summary>
public class WasmDecodeException : WasmException
{
    public WasmDecodeException() { }

    public WasmDecodeException(string? message)
        : base(message) { }

    public WasmDecodeException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
