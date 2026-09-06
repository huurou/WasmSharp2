namespace WasmSharp.Exceptions;

/// <summary>
/// Invoke時例外
/// </summary>
public class WasmInvokeException : WasmException
{
    public WasmInvokeException() { }

    public WasmInvokeException(string? message)
        : base(message) { }

    public WasmInvokeException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
