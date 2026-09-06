using System.Collections.Immutable;

namespace WasmSharp;

/// <summary>
/// functionの呼び出し結果となるvalueのコレクション
/// </summary>
/// <param name="values">構築時にコピーする戻り値のコレクション</param>
public sealed class WasmResults(ReadOnlySpan<WasmValue> values)
{
    /// <summary>
    /// 呼び出し結果を保持する不変配列
    /// </summary>
    public ImmutableArray<WasmValue> Values { get; } = ImmutableArray.Create(values);
}
