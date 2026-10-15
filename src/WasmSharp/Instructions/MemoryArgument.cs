namespace WasmSharp.Instructions;

/// <summary>
/// メモリ命令のアラインメント指数とオフセット
/// </summary>
/// <param name="Alignment">アラインメントを2の累乗で表す指数</param>
/// <param name="Offset">符号なし32ビットのオフセット</param>
internal readonly record struct MemoryArgument(uint Alignment, uint Offset);
