namespace WasmSharp;

/// <summary>
/// globalの値型と可変性
/// </summary>
/// <param name="ValueKind">globalに保持する値の種類</param>
/// <param name="IsMutable">値を更新できるかどうか</param>
public sealed record WasmGlobalType(WasmValueKind ValueKind, bool IsMutable)
{
    /// <summary>
    /// 参照先の型とnull可否を含むglobalの値型
    /// </summary>
    public WasmValueType ValueType { get; init; } = ValueKind;

    /// <summary>
    /// globalに保持する値の種類
    /// </summary>
    public WasmValueKind ValueKind
    {
        get => ValueType.Kind;
        init => ValueType = value;
    }

    /// <summary>
    /// 値型と可変性を指定してglobalの型を構築する
    /// </summary>
    public WasmGlobalType(WasmValueType valueType, bool isMutable)
        : this(valueType.Kind, isMutable)
    {
        ValueType = valueType;
    }

    /// <summary>
    /// import宣言が提供されたglobalの型を受け入れるかを返す
    /// </summary>
    internal bool Accepts(WasmGlobalType provided)
    {
        return IsMutable == provided.IsMutable
            && (IsMutable ? ValueType.Equals(provided.ValueType) : ValueType.Accepts(provided.ValueType));
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"WasmGlobalType {{ ValueKind = {ValueType}, IsMutable = {IsMutable} }}";
    }
}
