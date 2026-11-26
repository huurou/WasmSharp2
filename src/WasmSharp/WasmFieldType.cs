namespace WasmSharp;

/// <summary>
/// フィールドの格納型と可変性
/// </summary>
public readonly struct WasmFieldType
{
    /// <summary>
    /// 命令のオペランドに用いる値型 i8とi16のフィールドではi32を返す
    /// </summary>
    public WasmValueType ValueType { get; }

    /// <summary>
    /// 初期化後に値を変更できるかどうか
    /// </summary>
    public bool IsMutable { get; }

    /// <summary>
    /// i32を狭い幅で格納する場合の型
    /// </summary>
    public WasmPackedType PackedType { get; }

    internal WasmFieldType(WasmValueType valueType, bool isMutable, WasmPackedType packedType)
    {
        ValueType = valueType;
        IsMutable = isMutable;
        PackedType = packedType;
    }

    internal bool Accepts(WasmFieldType child)
    {
        return IsMutable == child.IsMutable
            && PackedType == child.PackedType
            && (IsMutable ? ValueType == child.ValueType : ValueType.Accepts(child.ValueType));
    }
}
