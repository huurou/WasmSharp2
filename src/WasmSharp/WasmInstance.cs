namespace WasmSharp;

/// <summary>
/// instanceを表現するクラス
/// </summary>
/// <remarks>Exportsは持たせない</remarks>
public sealed class WasmInstance
{
    /// <summary>
    /// 指定したexport名の関数を取得する
    /// </summary>
    /// <param name="name">関数のexport名</param>
    /// <returns>呼び出し対象の関数</returns>
    public WasmFunction GetFunction(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のglobalを取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>globalに格納されたvalue</returns>
    public WasmValue GetGlobal(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のmemoryを取得する
    /// </summary>
    /// <param name="name">memoryのexport名</param>
    /// <returns>memoru</returns>
    public WasmMemory GetMemory(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のtableを取得する
    /// </summary>
    /// <param name="name">tableのexport名</param>
    /// <returns>table</returns>
    public WasmTable GetTable(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のtagを取得する
    /// </summary>
    /// <param name="name">tagのexport名</param>
    /// <returns>tag</returns>
    public WasmTag GetTag(string name)
    {
        throw new NotImplementedException();
    }
}
