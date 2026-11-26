using System.Collections.Immutable;
using System.Text;
using WasmSharp.Modules.Definitions;

namespace WasmSharp;

/// <summary>
/// Wasmの構造体型 グループ内の型番号を解決したフィールドを宣言順に保持する
/// </summary>
public sealed class WasmStructType : WasmDefinedType
{
    /// <summary>
    /// 型の検証と格納に使用するフィールドの宣言
    /// </summary>
    public ImmutableArray<WasmFieldType> Fields { get; private set; }

    internal bool IsDefaultable { get; private set; }
    internal override WasmValueKind ReferenceKind => WasmValueKind.StructRef;

    internal WasmStructType(WasmDefinedType[]? group, int position, bool isFinal)
        : base(group, position, isFinal) { }

    private protected override void SetDecodedComposite(
        TypeDeclaration declaration,
        int firstIndex,
        ReadOnlySpan<WasmDefinedType> members
    )
    {
        var fields = declaration.Fields;
        var isDefaultable = true;
        ImmutableArray<WasmFieldType>.Builder? resolved = null;
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            isDefaultable &= field.ValueType is not { IsReference: true, IsNullable: false };
            if (ResolveReference(field.ValueType, firstIndex, members) is { } valueType)
            {
                resolved ??= fields.ToBuilder();
                resolved[index] = new WasmFieldType(valueType, field.IsMutable, field.PackedType);
            }
        }
        Fields = resolved?.MoveToImmutable() ?? fields;
        IsDefaultable = isDefaultable;
    }

    private protected override bool MatchesSupertype(WasmDefinedType parent)
    {
        if (parent is not WasmStructType structure || Fields.Length < structure.Fields.Length)
        {
            return false;
        }
        for (var index = 0; index < structure.Fields.Length; index++)
        {
            if (!structure.Fields[index].Accepts(Fields[index]))
            {
                return false;
            }
        }
        return true;
    }

    private protected override string DescribeComposite(ref int remaining, ImmutableStack<WasmDefinedType> path)
    {
        var text = new StringBuilder("(struct");
        foreach (var field in Fields)
        {
            if (remaining == 0)
            {
                text.Append(" …");
                break;
            }
            var value = DescribeValue(field.ValueType, ref remaining, path);
            var storage = field.PackedType switch
            {
                WasmPackedType.I8 => "i8",
                WasmPackedType.I16 => "i16",
                _ => value,
            };
            text.Append(field.IsMutable ? $" (field (mut {storage}))" : $" (field {storage})");
        }
        return text.Append(')').ToString();
    }
}
