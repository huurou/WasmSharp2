namespace WasmSharp;

/// <summary>
/// Wasmインスタンス 実行時の実体
/// </summary>
/// <remarks>Exportsは持たせない</remarks>
public sealed class WasmInstance
{
    public WasmFunction GetFunction(string name)
    {
        throw new NotImplementedException();
    }

    public WasmValue GetGlobal(string name)
    {
        throw new NotImplementedException();
    }

    public WasmMemory GetMemory(string name)
    {
        throw new NotImplementedException();
    }

    public WasmTable GetTable(string name)
    {
        throw new NotImplementedException();
    }

    public WasmTag GetTag(string name)
    {
        throw new NotImplementedException();
    }
}
