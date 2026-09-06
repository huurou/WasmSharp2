namespace WasmSharp;

/// <summary>
/// valueを表現するクラス
/// </summary>
public readonly struct WasmValue
{
    /// <summary>
    /// v128の下位64ビットまたはスカラー値
    /// </summary>
    private readonly ulong low64_;

    /// <summary>
    /// v128の上位64ビット
    /// </summary>
    private readonly ulong high64_;

    /// <summary>
    /// 参照値の参照先
    /// </summary>
    private readonly object? reference_;

    /// <summary>
    /// 格納されたvalueの種類
    /// </summary>
    private readonly WasmValueKind kind_;
}
