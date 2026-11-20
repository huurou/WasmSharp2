namespace WasmSharp.Instructions;

/// <summary>
/// メモリ命令のアラインメント指数とオフセット
/// </summary>
/// <remarks>入力命令と実行命令では2つの値を別々のフィールドに格納し、この構造体のパディングが命令ごとに加わるのを防ぐ。</remarks>
/// <param name="Alignment">アラインメントを2の累乗で表す指数</param>
/// <param name="Offset">符号なしのオフセット 32ビットメモリでの上限は検証時に確認する</param>
internal readonly record struct MemoryArgument(uint Alignment, ulong Offset);
