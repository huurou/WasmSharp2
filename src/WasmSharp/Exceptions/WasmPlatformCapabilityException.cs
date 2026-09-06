namespace WasmSharp.Exceptions;

/// <summary>
/// プラットフォーム側に問題があった時の例外 CPU機能などが利用できない等
/// </summary>
public class WasmPlatformCapabilityException : WasmException
{
    public WasmPlatformCapabilityException() { }

    public WasmPlatformCapabilityException(string? message)
        : base(message) { }

    public WasmPlatformCapabilityException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
