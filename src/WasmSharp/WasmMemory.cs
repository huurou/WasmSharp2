using WasmSharp.Exceptions;

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
    /// ページ単位の記憶領域
    /// </summary>
    private byte[][] _pages;

    /// <summary>
    /// 宣言されたアドレス型と初期・最大ページ数
    /// </summary>
    public WasmMemoryType Type { get; }

    /// <summary>
    /// 現在のページ数
    /// </summary>
    public uint PageCount => (uint)_pages.Length;

    /// <summary>
    /// メモリのアドレス型にかかわらず、宣言された最大ページ数を32ビットの値で返す。上限を省略した場合はnullを返す
    /// </summary>
    /// <remarks>アドレス型と64ビット精度の最大ページ数は<see cref="Type"/>から取得できる</remarks>
    /// <exception cref="InvalidOperationException">最大ページ数が32ビットで表せない場合</exception>
    public uint? MaximumPages =>
        Type.MaximumPages is > uint.MaxValue
            ? throw new InvalidOperationException(
                "最大ページ数が32ビットで表せません。Type.MaximumPagesから取得してください。"
            )
            : (uint?)Type.MaximumPages;

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
        : this(new WasmMemoryType(limits ?? throw new ArgumentNullException(nameof(limits))), nameof(limits)) { }

    private WasmMemory(WasmMemoryType type, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(type, parameterName);
        // Stryker disable once Logical,Boolean,Statement,Block: 上限判定を反転するか例外を取り除くと、境界値テストが65,537ページ（約4GiB）を確保する。
        if (type.MaximumPages > type.PageLimit || ExceedsPageLimit(type.MinimumPages, type))
        {
            throw new ArgumentException("memoryのページ数がアドレス型の制約を満たしません。", parameterName);
        }

        if (type.MinimumPages > (ulong)Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "memoryの初期ページ数がページ表の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength
            );
        }

        Type = type;
        _pages = new byte[(int)type.MinimumPages][];
        for (var index = 0; index < _pages.Length; index++)
        {
            _pages[index] = new byte[PAGE_SIZE];
        }
    }

    /// <summary>
    /// 指定したアドレス型とページ数でゼロ初期化されたメモリを生成する
    /// </summary>
    /// <param name="type">宣言されたアドレス型とページ数の上下限</param>
    /// <returns>最小ページ数に従って初期化したメモリ</returns>
    /// <exception cref="ArgumentNullException">typeがnullの場合</exception>
    /// <exception cref="ArgumentException">ページ数の上下限がアドレス型の制約を満たさない場合</exception>
    /// <exception cref="WasmImplementationLimitException">初期ページ数がページ表の保持上限を超える場合</exception>
    /// <exception cref="OutOfMemoryException">初期領域を割り当てられない場合</exception>
    public static WasmMemory Create(WasmMemoryType type)
    {
        return new WasmMemory(type, nameof(type));
    }

    /// <summary>
    /// 指定範囲へホスト側バッファの内容をコピーする
    /// </summary>
    /// <param name="offset">書き込み先の先頭バイト位置</param>
    /// <param name="source">全体をコピーするホスト側バッファ</param>
    /// <exception cref="ArgumentOutOfRangeException">書き込み範囲が現在のmemoryの外にある場合 内容は変更しない</exception>
    public void Write(ulong offset, ReadOnlySpan<byte> source)
    {
        if (!TryWrite(offset, source))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "memoryの範囲外です。");
        }
    }

    /// <summary>
    /// 指定範囲をホスト側バッファへコピーする
    /// </summary>
    /// <remarks>コピーしたバイトは、その後のmemoryの更新や増大に追従しない</remarks>
    /// <param name="offset">読み出し元の先頭バイト位置</param>
    /// <param name="destination">その長さだけ読み出したバイトを受け取るホスト側バッファ</param>
    /// <exception cref="ArgumentOutOfRangeException">読み出し範囲が現在のmemoryの外にある場合 destinationは変更しない</exception>
    public void Read(ulong offset, Span<byte> destination)
    {
        if (!TryRead(offset, destination))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "memoryの範囲外です。");
        }
    }

    /// <summary>
    /// 既存内容を保持して増大し、追加ページをゼロ初期化する
    /// </summary>
    /// <param name="deltaPages">追加するページ数 0の場合も成功する</param>
    /// <param name="previousPageCount">成功・失敗にかかわらず、増大前のページ数</param>
    /// <returns>増大に成功した場合はtrue 宣言・アドレス型・ページ表の上限を超える場合は、サイズと内容を変更せずfalse</returns>
    /// <exception cref="OutOfMemoryException">追加領域を割り当てられない場合 サイズと内容は変更しない</exception>
    public bool TryGrow(uint deltaPages, out uint previousPageCount)
    {
        return TryGrow((ulong)deltaPages, out previousPageCount);
    }

    /// <inheritdoc cref="TryGrow(uint, out uint)"/>
    internal bool TryGrow(ulong deltaPages, out uint previousPageCount)
    {
        previousPageCount = PageCount;
        // 64ビット増分との加算がオーバーフローしないよう、ページ表に追加できる数と先に比較する。
        if (deltaPages > (ulong)Array.MaxLength - previousPageCount)
        {
            return false;
        }

        var nextPageCount = previousPageCount + deltaPages;
        // Stryker disable once Boolean: 上限判定を反転すると、拡張を拒否するテストで4GiB以上の領域を確保する。
        if (ExceedsPageLimit(nextPageCount, Type))
        {
            return false;
        }

        if (deltaPages == 0)
        {
            return true;
        }

        // 追加領域の割当が全件成功するまで、既存のページ表を変更しない。
        var pages = new byte[(int)nextPageCount][];
        _pages.CopyTo(pages, 0);
        for (var index = _pages.Length; index < pages.Length; index++)
        {
            pages[index] = new byte[PAGE_SIZE];
        }

        _pages = pages;
        return true;
    }

    /// <summary>
    /// ページ数が仕様上限または宣言された最大値を超えるかを返す
    /// </summary>
    /// <param name="pageCount">割当前に判定するページ数</param>
    /// <param name="type">アドレス型と宣言された上下限</param>
    /// <returns>いずれかの上限を超える場合はtrue</returns>
    internal static bool ExceedsPageLimit(ulong pageCount, WasmMemoryType type)
    {
        var exceedsDeclaredMaximum = pageCount > type.MaximumPages;
        // Stryker disable once Logical,Equality: 仕様上限を超えた要求を通すと、生成時や拡張時に4GiB以上の領域を確保する。
        return pageCount > type.PageLimit || exceedsDeclaredMaximum;
    }

    /// <summary>
    /// 指定した全範囲が現在のmemoryに収まるかを返す
    /// </summary>
    /// <param name="offset">範囲の先頭バイト位置 長さが0の場合も検査する</param>
    /// <param name="length">範囲のバイト数</param>
    /// <returns>全範囲が現在のmemoryに収まる場合はtrue</returns>
    internal bool ContainsRange(ulong offset, ulong length)
    {
        // 加算のオーバーフローを避け、コピー前に全範囲を検査する。
        return offset <= ByteLength && length <= ByteLength - offset;
    }

    /// <summary>
    /// 指定範囲をホスト側バッファへコピーし、範囲内かを返す
    /// </summary>
    /// <param name="offset">読み出し元の先頭バイト位置</param>
    /// <param name="destination">その長さだけ読み出したバイトを受け取るホスト側バッファ</param>
    /// <returns>全範囲を転送できた場合はtrue 範囲外の場合はdestinationを変更せずfalse</returns>
    internal bool TryRead(ulong offset, Span<byte> destination)
    {
        if (!ContainsRange(offset, (ulong)destination.Length))
        {
            return false;
        }

        while (!destination.IsEmpty)
        {
            var pageOffset = (int)(offset % PAGE_SIZE);
            var length = Math.Min(PAGE_SIZE - pageOffset, destination.Length);
            _pages[(int)(offset / PAGE_SIZE)].AsSpan(pageOffset, length).CopyTo(destination);
            offset += (uint)length;
            destination = destination[length..];
        }

        return true;
    }

    /// <summary>
    /// 指定範囲へホスト側バッファの内容をコピーし、範囲内かを返す
    /// </summary>
    /// <param name="offset">書き込み先の先頭バイト位置</param>
    /// <param name="source">全体をコピーするホスト側バッファ</param>
    /// <returns>全範囲を転送できた場合はtrue 範囲外の場合はmemoryを変更せずfalse</returns>
    internal bool TryWrite(ulong offset, ReadOnlySpan<byte> source)
    {
        if (!ContainsRange(offset, (ulong)source.Length))
        {
            return false;
        }

        while (!source.IsEmpty)
        {
            var pageOffset = (int)(offset % PAGE_SIZE);
            var length = Math.Min(PAGE_SIZE - pageOffset, source.Length);
            source[..length].CopyTo(_pages[(int)(offset / PAGE_SIZE)].AsSpan(pageOffset, length));
            offset += (uint)length;
            source = source[length..];
        }

        return true;
    }

    /// <summary>
    /// 指定メモリから範囲をコピーし、範囲内かを返す
    /// </summary>
    /// <param name="sourceMemory">コピー元のメモリ</param>
    /// <param name="destination">コピー先の先頭バイト位置</param>
    /// <param name="source">コピー元の先頭バイト位置</param>
    /// <param name="length">コピーするバイト数</param>
    /// <returns>全範囲を転送できた場合はtrue 範囲外の場合はmemoryを変更せずfalse</returns>
    internal bool TryCopy(WasmMemory sourceMemory, ulong destination, ulong source, ulong length)
    {
        if (!ContainsRange(destination, length) || !sourceMemory.ContainsRange(source, length))
        {
            return false;
        }

        switch (GetCopyDirection(sourceMemory, destination, source))
        {
            case MemoryCopyDirection.Forward:
                CopyForward(sourceMemory, destination, source, length);
                break;
            case MemoryCopyDirection.Backward:
                // 別々のインデックスが同じメモリを指す場合も、未転送のバイトを上書きしないよう末尾からコピーする。
                CopyBackward(destination + length, source + length, length);
                break;
        }

        return true;
    }

    /// <summary>
    /// メモリの同一性と開始位置から、コピー方向またはコピーの省略を決める
    /// </summary>
    /// <param name="sourceMemory">コピー元のメモリ</param>
    /// <param name="destination">コピー先の先頭バイト位置</param>
    /// <param name="source">コピー元の先頭バイト位置</param>
    /// <returns>コピー方向 同じメモリの同じ位置ならNone</returns>
    internal MemoryCopyDirection GetCopyDirection(WasmMemory sourceMemory, ulong destination, ulong source)
    {
        if (!ReferenceEquals(this, sourceMemory) || destination < source)
        {
            return MemoryCopyDirection.Forward;
        }

        return destination > source ? MemoryCopyDirection.Backward : MemoryCopyDirection.None;
    }

    /// <summary>
    /// コピー先が後ろにある場合は、重なっているコピー元を保つため、両範囲の終端から転送する
    /// </summary>
    /// <param name="destinationEnd">コピー先の最終バイトの次の位置</param>
    /// <param name="sourceEnd">コピー元の最終バイトの次の位置</param>
    /// <param name="length">範囲確認済みの転送バイト数</param>
    private void CopyBackward(ulong destinationEnd, ulong sourceEnd, ulong length)
    {
        while (length != 0)
        {
            var destinationOffset = (int)((destinationEnd - 1) % PAGE_SIZE) + 1;
            var sourceOffset = (int)((sourceEnd - 1) % PAGE_SIZE) + 1;
            var count = (int)Math.Min(length, (ulong)Math.Min(destinationOffset, sourceOffset));
            _pages[(int)((sourceEnd - 1) / PAGE_SIZE)]
                .AsSpan(sourceOffset - count, count)
                .CopyTo(_pages[(int)((destinationEnd - 1) / PAGE_SIZE)].AsSpan(destinationOffset - count, count));
            destinationEnd -= (uint)count;
            sourceEnd -= (uint)count;
            length -= (uint)count;
        }
    }

    /// <summary>
    /// コピー元とコピー先の範囲の先頭から転送する
    /// </summary>
    /// <param name="sourceMemory">コピー元のメモリ</param>
    /// <param name="destination">コピー先の先頭バイト位置</param>
    /// <param name="source">コピー元の先頭バイト位置</param>
    /// <param name="length">範囲確認済みの転送バイト数</param>
    private void CopyForward(WasmMemory sourceMemory, ulong destination, ulong source, ulong length)
    {
        while (length != 0)
        {
            var destinationOffset = (int)(destination % PAGE_SIZE);
            var sourceOffset = (int)(source % PAGE_SIZE);
            var count = (int)Math.Min(length, (ulong)Math.Min(PAGE_SIZE - destinationOffset, PAGE_SIZE - sourceOffset));
            sourceMemory
                ._pages[(int)(source / PAGE_SIZE)]
                .AsSpan(sourceOffset, count)
                .CopyTo(_pages[(int)(destination / PAGE_SIZE)].AsSpan(destinationOffset, count));
            destination += (uint)count;
            source += (uint)count;
            length -= (uint)count;
        }
    }

    /// <summary>
    /// 指定範囲をバイト値で埋め、範囲内かを返す
    /// </summary>
    /// <param name="destination">書き込み先の先頭バイト位置</param>
    /// <param name="value">各バイトに書き込む値</param>
    /// <param name="length">書き込むバイト数</param>
    /// <returns>全範囲を書き込めた場合はtrue 範囲外の場合はmemoryを変更せずfalse</returns>
    internal bool TryFill(ulong destination, byte value, ulong length)
    {
        if (!ContainsRange(destination, length))
        {
            return false;
        }

        while (length != 0)
        {
            var pageOffset = (int)(destination % PAGE_SIZE);
            var count = (int)Math.Min(length, (ulong)(PAGE_SIZE - pageOffset));
            _pages[(int)(destination / PAGE_SIZE)].AsSpan(pageOffset, count).Fill(value);
            destination += (uint)count;
            length -= (uint)count;
        }

        return true;
    }
}

/// <summary>
/// メモリのコピーでページをたどる方向
/// </summary>
internal enum MemoryCopyDirection
{
    /// <summary>
    /// コピーを省略する
    /// </summary>
    None,

    /// <summary>
    /// 範囲の先頭からコピーする
    /// </summary>
    Forward,

    /// <summary>
    /// 範囲の末尾からコピーする
    /// </summary>
    Backward,
}
