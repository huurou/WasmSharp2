namespace WasmSharp;

public delegate Span<WasmValue> WasmHostCallback(ReadOnlySpan<WasmValue> arguments);

public sealed class WasmHostModule { }
