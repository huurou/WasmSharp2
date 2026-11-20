namespace WasmSharp.Instructions;

/// <summary>
/// メモリ命令のアラインメント指数とオフセット
/// </summary>
/// <param name="Alignment">アラインメントを2の累乗で表す指数</param>
/// <param name="Offset">符号なしのオフセット 32ビットメモリでの上限は検証時に確認する</param>
internal readonly record struct MemoryArgument(uint Alignment, ulong Offset);
