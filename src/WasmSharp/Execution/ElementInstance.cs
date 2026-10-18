namespace WasmSharp.Execution;

/// <summary>
/// インスタンスごとの評価済み要素列と破棄状態
/// </summary>
/// <param name="values">要素型が検証済みの参照列</param>
internal sealed class ElementInstance(ReadOnlySpan<WasmValue> values)
{
    /// <summary>
    /// 現在保持している参照列
    /// </summary>
    private WasmValue[] values_ = values.ToArray();

    /// <summary>
    /// 現在保持している要素数 破棄後は0
    /// </summary>
    internal uint Length => (uint)values_.Length;

    /// <summary>
    /// 指定範囲をtableへ転送し、供給元の参照列を保持する
    /// </summary>
    /// <param name="table">要素型が一致する転送先table</param>
    /// <param name="destination">table内のコピー先の先頭要素位置</param>
    /// <param name="source">要素列内のコピー元の先頭要素位置</param>
    /// <param name="length">コピーする要素数</param>
    /// <returns>転送に成功した場合はtrue いずれかの範囲が無効な場合はtableを変更せずfalse</returns>
    internal bool TryInitialize(WasmTable table, uint destination, uint source, uint length)
    {
        // 全範囲が配列に収まることを確かめてから、Span用のintへ変換する。
        if ((ulong)source + length > (ulong)values_.Length)
        {
            return false;
        }

        return table.TryWrite(destination, values_.AsSpan((int)source, (int)length));
    }

    /// <summary>
    /// 保持する参照列を空にする 繰り返し呼び出しても成功する
    /// </summary>
    internal void Drop()
    {
        values_ = [];
    }
}
