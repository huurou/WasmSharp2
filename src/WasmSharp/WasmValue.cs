namespace WasmSharp;

public readonly struct WasmValue
{
    /// <summary>
    /// v128のbytes 0..7 / スカラー値
    /// </summary>
    private readonly ulong low64_;

    /// <summary>
    /// v128のbytes 8..15
    /// </summary>
    private readonly ulong high64_;

    private readonly object? reference_;
    private readonly WasmValueKind kind_;
}
