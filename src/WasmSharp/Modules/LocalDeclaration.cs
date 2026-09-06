namespace WasmSharp.Modules;

/// <summary>
/// 展開前のlocalsの個数と型
/// </summary>
public readonly struct LocalDeclaration
{
    /// <summary>
    /// 同じ型で連続するlocalの個数
    /// </summary>
    internal uint Count { get; }

    /// <summary>
    /// 宣言されたlocalの値型
    /// </summary>
    internal WasmValueKind Type { get; }

    /// <summary>
    /// 同じ型のlocalを個数と型でまとめて構築する
    /// </summary>
    /// <param name="count">localの個数</param>
    /// <param name="type">localの値型</param>
    internal LocalDeclaration(uint count, WasmValueKind type)
    {
        Count = count;
        Type = type;
    }
}
