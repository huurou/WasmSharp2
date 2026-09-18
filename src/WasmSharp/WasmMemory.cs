namespace WasmSharp;

/// <summary>
/// memoryを表現するクラス
/// </summary>
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
    /// <remarks>予測可能な上限超過はfalseを返す。実割当例外は伝播し、既存状態を維持する</remarks>
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

    private void ValidateRange(ulong offset, int length)
    {
        // 加算のオーバーフローを避け、コピー前に全範囲を検査する。
        if (offset > ByteLength || (ulong)length > ByteLength - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "memoryの範囲外です。");
        }
    }
}
