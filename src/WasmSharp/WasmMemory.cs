namespace WasmSharp;

/// <summary>
/// バイト領域とページ数を保持する共有memory
/// </summary>
/// <remarks>同じ実体をimportしたinstanceとホストは、更新後の内容と増大後のサイズを共有する</remarks>
public sealed class WasmMemory
{
    /// <summary>
    /// 1ページのバイト数
    /// </summary>
    private const int PAGE_SIZE = 65536;

    /// <summary>
    /// Core 2.0の最大ページ数
    /// </summary>
    private const uint MAX_PAGE_COUNT = 65536;

    /// <summary>
    /// ページ単位の記憶領域
    /// </summary>
    private byte[][] pages_;

    /// <summary>
    /// 現在のページ数
    /// </summary>
    public uint PageCount => (uint)pages_.Length;

    /// <summary>
    /// 宣言された最大ページ数。指定がなければnull
    /// </summary>
    public uint? MaximumPages { get; }

    /// <summary>
    /// 現在のバイト長
    /// </summary>
    public ulong ByteLength => (ulong)PageCount * PAGE_SIZE;

    /// <summary>
    /// limitsに従ってゼロ初期化されたmemoryを生成する
    /// </summary>
    /// <param name="limits">1ページを65,536バイトとする初期ページ数と任意の最大ページ数</param>
    /// <exception cref="ArgumentNullException">limitsがnullの場合</exception>
    /// <exception cref="ArgumentException">最小値が最大値を超えるか、いずれかが65,536ページを超える場合</exception>
    /// <exception cref="OutOfMemoryException">初期領域を割り当てられない場合</exception>
    public WasmMemory(WasmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (
            limits.Minimum > MAX_PAGE_COUNT
            || limits.Maximum > MAX_PAGE_COUNT
            || limits.Minimum > limits.Maximum
        )
        {
            throw new ArgumentException(
                "memoryのlimitsがCore 2.0の制約を満たしません。",
                nameof(limits)
            );
        }

        pages_ = new byte[limits.Minimum][];
        for (var index = 0; index < pages_.Length; index++)
        {
            pages_[index] = new byte[PAGE_SIZE];
        }
        MaximumPages = limits.Maximum;
    }

    /// <summary>
    /// 指定範囲へホスト側バッファの内容をコピーする
    /// </summary>
    /// <param name="offset">書き込み先の先頭バイト位置</param>
    /// <param name="source">全体をコピーするホスト側バッファ</param>
    /// <exception cref="ArgumentOutOfRangeException">書き込み範囲が現在のmemoryの外にある場合。内容は変更しない</exception>
    public void Write(ulong offset, ReadOnlySpan<byte> source)
    {
        ValidateRange(offset, source.Length);
        while (!source.IsEmpty)
        {
            var pageOffset = (int)(offset % PAGE_SIZE);
            var length = Math.Min(PAGE_SIZE - pageOffset, source.Length);
            source[..length].CopyTo(pages_[(int)(offset / PAGE_SIZE)].AsSpan(pageOffset, length));
            offset += (uint)length;
            source = source[length..];
        }
    }

    /// <summary>
    /// 指定範囲をホスト側バッファへコピーする
    /// </summary>
    /// <remarks>コピーしたバイトは、その後のmemoryの更新や増大に追従しない</remarks>
    /// <param name="offset">読み出し元の先頭バイト位置</param>
    /// <param name="destination">その長さだけ読み出したバイトを受け取るホスト側バッファ</param>
    /// <exception cref="ArgumentOutOfRangeException">読み出し範囲が現在のmemoryの外にある場合。destinationは変更しない</exception>
    public void Read(ulong offset, Span<byte> destination)
    {
        ValidateRange(offset, destination.Length);
        while (!destination.IsEmpty)
        {
            var pageOffset = (int)(offset % PAGE_SIZE);
            var length = Math.Min(PAGE_SIZE - pageOffset, destination.Length);
            pages_[(int)(offset / PAGE_SIZE)].AsSpan(pageOffset, length).CopyTo(destination);
            offset += (uint)length;
            destination = destination[length..];
        }
    }

    /// <summary>
    /// 既存内容を保持して増大し、追加ページをゼロ初期化する
    /// </summary>
    /// <param name="deltaPages">追加するページ数。0の場合も成功する</param>
    /// <param name="previousPageCount">成功・失敗にかかわらず、増大前のページ数</param>
    /// <returns>増大に成功した場合はtrue。宣言された最大値または65,536ページを超える場合は、サイズと内容を変更せずfalse</returns>
    /// <exception cref="OutOfMemoryException">追加領域を割り当てられない場合。サイズと内容は変更しない</exception>
    public bool TryGrow(uint deltaPages, out uint previousPageCount)
    {
        previousPageCount = PageCount;
        var nextPageCount = (ulong)previousPageCount + deltaPages;
        if (nextPageCount > MAX_PAGE_COUNT || nextPageCount > MaximumPages)
        {
            return false;
        }

        if (deltaPages == 0)
        {
            return true;
        }

        // 追加領域の割当が全件成功するまで、既存のページ表を変更しない。
        var pages = new byte[(int)nextPageCount][];
        pages_.CopyTo(pages, 0);
        for (var index = pages_.Length; index < pages.Length; index++)
        {
            pages[index] = new byte[PAGE_SIZE];
        }

        pages_ = pages;
        return true;
    }

    /// <summary>
    /// コピー対象の全範囲が現在のmemoryに収まることを確認する
    /// </summary>
    /// <param name="offset">範囲の先頭バイト位置</param>
    /// <param name="length">バッファから取得した非負のバイト数</param>
    /// <exception cref="ArgumentOutOfRangeException">範囲が現在のmemoryの外にある場合</exception>
    private void ValidateRange(ulong offset, int length)
    {
        // 加算のオーバーフローを避け、コピー前に全範囲を検査する。
        if (offset > ByteLength || (ulong)length > ByteLength - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "memoryの範囲外です。");
        }
    }
}
