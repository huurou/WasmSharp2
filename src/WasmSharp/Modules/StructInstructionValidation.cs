using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 構造体命令が参照する型とフィールドを検証する
/// </summary>
internal static class StructInstructionValidation
{
    /// <summary>
    /// 型番号が範囲内にあることを先に検証し、参照先が構造体型ならその定義を返す
    /// </summary>
    internal static WasmStructType RequireType(
        ReadOnlySpan<WasmDefinedType> types,
        uint typeIndex,
        long byteOffset,
        byte sectionId,
        uint? functionIndex = null
    )
    {
        if (typeIndex >= (uint)types.Length)
        {
            throw ValidationDiagnostics.UnknownIndex(
                "type",
                typeIndex,
                "参照する構造体型が存在しません。",
                CreateLocation(byteOffset, sectionId, functionIndex)
            );
        }
        return types[(int)typeIndex] as WasmStructType
            ?? throw ValidationDiagnostics.ValidationFailure(
                $"non-structure type {typeIndex}",
                "指定した型は構造体型ではありません。",
                CreateLocation(byteOffset, sectionId, functionIndex)
            );
    }

    /// <summary>
    /// struct.new_defaultがすべてのフィールドを既定値で初期化できることを検証する
    /// </summary>
    internal static void RequireDefaultable(WasmStructType type, long byteOffset, byte sectionId, uint? functionIndex = null)
    {
        if (!type.IsDefaultable)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "field type is not defaultable",
                "非nullの参照フィールドには初期値が必要です。",
                CreateLocation(byteOffset, sectionId, functionIndex)
            );
        }
    }

    /// <summary>
    /// フィールド番号の存在を先に検証し、書き込み時は可変性、読み取り時は命令と詰め込み形式の対応を確認する
    /// </summary>
    internal static WasmFieldType RequireField(
        WasmStructType type,
        uint fieldIndex,
        ValidationRule operation,
        long byteOffset,
        byte sectionId,
        uint? functionIndex = null
    )
    {
        if (fieldIndex >= (uint)type.Fields.Length)
        {
            throw ValidationDiagnostics.UnknownIndex(
                "field",
                fieldIndex,
                "指定したフィールドが存在しません。",
                CreateLocation(byteOffset, sectionId, functionIndex)
            );
        }
        var field = type.Fields[(int)fieldIndex];
        return operation switch
        {
            ValidationRule.StructSet when !field.IsMutable => throw ValidationDiagnostics.ValidationFailure(
                "field is immutable",
                "このフィールドは変更できません。",
                CreateLocation(byteOffset, sectionId, functionIndex)
            ),
            ValidationRule.StructGet when field.PackedType != WasmPackedType.None =>
                throw ValidationDiagnostics.ValidationFailure(
                    "field is packed",
                    "詰め込みフィールドの読み取りには拡張方法が必要です。",
                    CreateLocation(byteOffset, sectionId, functionIndex)
                ),
            ValidationRule.StructGetPacked when field.PackedType == WasmPackedType.None =>
                throw ValidationDiagnostics.ValidationFailure(
                    "field is unpacked",
                    "詰め込みフィールドだけを拡張できます。",
                    CreateLocation(byteOffset, sectionId, functionIndex)
                ),
            _ => field,
        };
    }

    private static WasmFailureLocation CreateLocation(long byteOffset, byte sectionId, uint? functionIndex)
    {
        return new WasmFailureLocation(WasmProcessingStage.Validate, byteOffset, functionIndex, sectionId);
    }
}
