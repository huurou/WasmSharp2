namespace WasmSharp;

public sealed class WasmFunction
{
    public WasmFunctionType Type => throw new NotImplementedException();

    public WasmResults Invoke(
        ReadOnlySpan<WasmValue> arguments,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
