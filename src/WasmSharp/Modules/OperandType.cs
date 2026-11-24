namespace WasmSharp.Modules;

/// <summary>
/// 検証スタック上の型 到達不能な位置では、任意の値型に適合するbottomと参照型だけに適合する(ref bot)を区別する
/// </summary>
internal readonly struct OperandType
{
    internal WasmValueType? DeclaredType { get; }

    internal bool IsReference { get; }

    private OperandType(WasmValueType? declaredType, bool isReference)
    {
        DeclaredType = declaredType;
        IsReference = isReference;
    }

    internal OperandType AsNonNullable()
    {
        return DeclaredType is { } type ? type.AsNonNullable() : new OperandType(null, true);
    }

    internal bool Matches(WasmValueType expected)
    {
        return DeclaredType is { } actual ? expected.Accepts(actual) : !IsReference || expected.IsReference;
    }

    public static implicit operator OperandType(WasmValueType? type)
    {
        return new OperandType(type, type is { IsReference: true });
    }

    public static implicit operator OperandType(WasmValueKind kind)
    {
        return (WasmValueType)kind;
    }
}
