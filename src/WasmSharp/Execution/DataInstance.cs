using System.Collections.Immutable;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Execution;

/// <summary>
/// インスタンスごとのデータセグメントの内容と破棄状態
/// </summary>
/// <param name="definition">内容を共有する不変のデータセグメント定義</param>
internal sealed class DataInstance(DataDefinition definition)
{
    /// <summary>
    /// 現在保持している不変のバイト列
    /// </summary>
    private ImmutableArray<byte> bytes_ = definition.Bytes;

    /// <summary>
    /// 現在保持しているバイト数 破棄後は0
    /// </summary>
    internal uint Length => (uint)bytes_.Length;

    /// <summary>
    /// 指定範囲をメモリへ転送し、データセグメントの内容を保持する
    /// </summary>
    /// <param name="memory">転送先のメモリ</param>
    /// <param name="destination">メモリ内のコピー先の先頭バイト位置</param>
    /// <param name="source">データセグメント内のコピー元の先頭バイト位置</param>
    /// <param name="length">コピーするバイト数</param>
    /// <returns>転送に成功した場合はtrue いずれかの範囲が無効な場合はメモリを変更せずfalse</returns>
    internal bool TryInitialize(WasmMemory memory, uint destination, uint source, uint length)
    {
        // 全範囲が配列に収まることを確かめてから、Span用のintへ変換する。
        if ((ulong)source + length > (ulong)bytes_.Length)
        {
            return false;
        }

        return memory.TryWrite(destination, bytes_.AsSpan((int)source, (int)length));
    }

    /// <summary>
    /// 内容への参照を空のバイト列へ置き換える 繰り返し呼び出しても成功する
    /// </summary>
    internal void Drop()
    {
        bytes_ = [];
    }
}
