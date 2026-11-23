namespace WasmSharp.Modules;

/// <summary>
/// 展開前のlocalsの宣言ごとの終端と型
/// </summary>
/// <param name="endIndex">引数を含めずに数えた、この宣言までのlocalの累積個数</param>
/// <param name="type">localの値型</param>
internal readonly struct LocalDeclaration(uint endIndex, WasmValueType type)
{
    /// <summary>
    /// この宣言の末尾の次を指す、引数を除いたlocalの添字
    /// </summary>
    internal uint EndIndex { get; } = endIndex;

    /// <summary>
    /// 宣言されたlocalの値型
    /// </summary>
    internal WasmValueType Type { get; } = type;
}
