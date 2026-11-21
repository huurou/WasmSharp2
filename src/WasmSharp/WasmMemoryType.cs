namespace WasmSharp;

/// <summary>
/// 線形メモリのアドレス型と、ページ数の最小値・最大値
/// </summary>
/// <remarks>宣言の大小関係や仕様上限はValidateまたはメモリ生成時に検証する</remarks>
/// <param name="AddressType">アドレスを表す整数型</param>
/// <param name="MinimumPages">最小ページ数</param>
/// <param name="MaximumPages">最大ページ数 指定しない場合はnull</param>
public sealed record WasmMemoryType(WasmValueKind AddressType, ulong MinimumPages, ulong? MaximumPages = null)
{
    /// <summary>
    /// アドレスを表すi32またはi64
    /// </summary>
    public WasmValueKind AddressType { get; } =
        AddressType is WasmValueKind.I32 or WasmValueKind.I64
            ? AddressType
            : throw new ArgumentOutOfRangeException(
                nameof(AddressType),
                AddressType,
                "メモリのアドレス型はi32またはi64です。"
            );

    /// <summary>
    /// アドレス型の仕様で許される最大ページ数
    /// </summary>
    internal ulong PageLimit => AddressType == WasmValueKind.I32 ? 1UL << 16 : 1UL << 48;

    /// <summary>
    /// 従来の32ビットメモリのlimitsでアドレス型とページ数を表現できるかどうか
    /// </summary>
    internal bool Has32BitLimits =>
        AddressType == WasmValueKind.I32 && MinimumPages <= uint.MaxValue && MaximumPages is not > uint.MaxValue;

    internal WasmMemoryType(WasmLimits limits)
        : this(WasmValueKind.I32, limits.Minimum, limits.Maximum) { }

    internal WasmLimits Get32BitLimits()
    {
        if (!Has32BitLimits)
        {
            throw new InvalidOperationException(
                "32ビットメモリのlimitsで表せない宣言です。Typeからメモリの型を取得してください。"
            );
        }
        return new WasmLimits((uint)MinimumPages, (uint?)MaximumPages);
    }
}
