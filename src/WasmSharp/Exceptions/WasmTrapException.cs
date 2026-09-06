namespace WasmSharp.Exceptions;

/// <summary>
/// 実行中のtrap時例外
/// </summary>
public class WasmTrapException : WasmException
{
    public WasmTrapException() { }

    public WasmTrapException(string? message)
        : base(message) { }

    public WasmTrapException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
