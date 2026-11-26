namespace WasmSharp;

/// <summary>
/// 生成時の具体型と、宣言順のフィールド値を保持する構造体の実体
/// </summary>
internal sealed class WasmStruct
{
    private readonly WasmValue[] _fields;

    internal WasmStructType Type { get; }

    private WasmStruct(WasmStructType type)
    {
        Type = type;
        _fields = type.Fields.IsEmpty ? [] : new WasmValue[type.Fields.Length];
    }

    /// <summary>
    /// 宣言順のオペランドから新しい構造体参照を生成する
    /// </summary>
    internal static WasmValue Create(WasmStructType type, ReadOnlySpan<WasmValue> fields)
    {
        var structure = new WasmStruct(type);
        for (var index = 0; index < fields.Length; index++)
        {
            structure.SetField(index, fields[index]);
        }
        return WasmValue.FromStruct(structure);
    }

    /// <summary>
    /// 各フィールドを型別の既定値で初期化し、新しい構造体参照を返す
    /// </summary>
    internal static WasmValue CreateDefault(WasmStructType type)
    {
        var structure = new WasmStruct(type);
        for (var index = 0; index < type.Fields.Length; index++)
        {
            structure._fields[index] = WasmValue.CreateInitialValue(type.Fields[index].ValueType.Kind);
        }
        return WasmValue.FromStruct(structure);
    }

    /// <summary>
    /// 詰め込みフィールドは下位8ビットまたは16ビットをゼロ拡張して格納する
    /// </summary>
    internal void SetField(int index, WasmValue value)
    {
        _fields[index] = Type.Fields[index].PackedType switch
        {
            WasmPackedType.I8 => WasmValue.FromI32(value.AsI32() & 0xFF),
            WasmPackedType.I16 => WasmValue.FromI32(value.AsI32() & 0xFFFF),
            _ => value,
        };
    }

    internal WasmValue GetField(int index, bool signed)
    {
        var value = _fields[index];
        if (!signed)
        {
            return value;
        }

        return Type.Fields[index].PackedType == WasmPackedType.I8
            ? WasmValue.FromI32(unchecked((sbyte)value.AsI32()))
            : WasmValue.FromI32(unchecked((short)value.AsI32()));
    }
}
