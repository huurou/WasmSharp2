using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 構造体命令が参照する型とフィールドを検証する
/// </summary>
internal static class StructInstructionValidation
{
    internal static WasmStructType RequireType(
        ReadOnlySpan<WasmDefinedType> types,
        uint typeIndex,
        WasmFailureLocation location
    )
    {
        if (typeIndex >= (uint)types.Length)
        {
            throw ValidationDiagnostics.UnknownIndex("type", typeIndex, "参照する構造体型が存在しません。", location);
        }
        return types[(int)typeIndex] as WasmStructType
            ?? throw ValidationDiagnostics.ValidationFailure(
                $"non-structure type {typeIndex}",
                "指定した型は構造体型ではありません。",
                location
            );
    }

    internal static void RequireDefaultable(WasmStructType type, WasmFailureLocation location)
    {
        if (!type.IsDefaultable)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "field type is not defaultable",
                "非nullの参照フィールドには初期値が必要です。",
                location
            );
        }
    }

    internal static WasmFieldType RequireField(
        WasmStructType type,
        uint fieldIndex,
        ValidationRule operation,
        WasmFailureLocation location
    )
    {
        if (fieldIndex >= (uint)type.Fields.Length)
        {
            throw ValidationDiagnostics.UnknownIndex("field", fieldIndex, "指定したフィールドが存在しません。", location);
        }
        var field = type.Fields[(int)fieldIndex];
        return operation switch
        {
            ValidationRule.StructSet when !field.IsMutable => throw ValidationDiagnostics.ValidationFailure(
                "field is immutable",
                "このフィールドは変更できません。",
                location
            ),
            ValidationRule.StructGet when field.PackedType != WasmPackedType.None =>
                throw ValidationDiagnostics.ValidationFailure(
                    "field is packed",
                    "詰め込みフィールドの読み取りには拡張方法が必要です。",
                    location
                ),
            ValidationRule.StructGetPacked when field.PackedType == WasmPackedType.None =>
                throw ValidationDiagnostics.ValidationFailure(
                    "field is unpacked",
                    "詰め込みフィールドだけを拡張できます。",
                    location
                ),
            _ => field,
        };
    }
}
