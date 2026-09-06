namespace WasmSharp.Exceptions;

/// <summary>
/// Validate時例外 型規則・構造規則に違反等
/// </summary>
public class WasmValidateException : WasmException
{
    public WasmValidateException() { }

    public WasmValidateException(string? message)
        : base(message) { }

    public WasmValidateException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
