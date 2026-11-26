namespace WasmSharp;

/// <summary>
/// テーブルの要素型とアドレス型、要素数の最小値・最大値
/// </summary>
/// <remarks>宣言の大小関係や要素数の仕様上限はValidateまたはテーブル生成時に検証する</remarks>
/// <param name="ElementType">要素の参照型</param>
/// <param name="AddressType">アドレスを表す整数型</param>
/// <param name="MinimumElements">最小要素数</param>
/// <param name="MaximumElements">最大要素数 指定しない場合はnull</param>
public sealed record WasmTableType(
    WasmValueKind ElementType,
    WasmValueKind AddressType,
    ulong MinimumElements,
    ulong? MaximumElements = null
)
{
    /// <summary>
    /// 要素の参照型
    /// </summary>
    public WasmValueKind ElementType => ElementValueType.Kind;

    /// <summary>
    /// 参照先の関数型とnull可否を含む要素型
    /// </summary>
    public WasmValueType ElementValueType { get; } =
        ElementType.IsReference()
            ? ElementType
            : throw new ArgumentOutOfRangeException(
                nameof(ElementType),
                ElementType,
                "テーブルの要素型には参照型を指定してください。"
            );

    /// <summary>
    /// アドレスを表すi32またはi64
    /// </summary>
    public WasmValueKind AddressType { get; } =
        AddressType is WasmValueKind.I32 or WasmValueKind.I64
            ? AddressType
            : throw new ArgumentOutOfRangeException(
                nameof(AddressType),
                AddressType,
                "テーブルのアドレス型はi32またはi64です。"
            );

    /// <summary>
    /// 宣言された要素数がアドレス型の上限を超えているかを返す
    /// </summary>
    internal bool ExceedsElementLimit =>
        AddressType == WasmValueKind.I32 && (MinimumElements > uint.MaxValue || MaximumElements > uint.MaxValue);

    /// <summary>
    /// 従来の32ビットテーブルのlimitsでアドレス型と要素数を表せるかを返す
    /// </summary>
    internal bool Has32BitLimits => AddressType == WasmValueKind.I32 && !ExceedsElementLimit;

    /// <summary>
    /// 型付き参照を保持するテーブルの要素型と上下限を指定する
    /// </summary>
    public WasmTableType(
        WasmValueType elementValueType,
        WasmValueKind addressType,
        ulong minimumElements,
        ulong? maximumElements = null
    )
        : this(elementValueType.Kind, addressType, minimumElements, maximumElements)
    {
        ElementValueType = elementValueType;
    }

    internal WasmTableType(WasmValueKind elementType, WasmLimits limits)
        : this(elementType, WasmValueKind.I32, limits.Minimum, limits.Maximum) { }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"WasmTableType {{ MinimumElements = {MinimumElements}, MaximumElements = {MaximumElements}, ElementType = {ElementValueType}, AddressType = {AddressType} }}";
    }

    internal WasmLimits Get32BitLimits()
    {
        if (!Has32BitLimits)
        {
            throw new InvalidOperationException(
                "32ビットテーブルのlimitsで表せない宣言です。Typeからテーブルの型を取得してください。"
            );
        }
        return new WasmLimits((uint)MinimumElements, (uint?)MaximumElements);
    }
}
