namespace WasmSharp.Modules;

/// <summary>
/// 展開前のlocalsの個数と型
/// </summary>
/// <remarks>同じ型のlocalを個数と型でまとめて構築する</remarks>
/// <param name="count">localの個数</param>
/// <param name="type">localの値型</param>
internal readonly struct LocalDeclaration(uint count, WasmValueKind type)
{
    /// <summary>
    /// 同じ型で連続するlocalの個数
    /// </summary>
    internal uint Count { get; } = count;

    /// <summary>
    /// 宣言されたlocalの値型
    /// </summary>
    internal WasmValueKind Type { get; } = type;
}
