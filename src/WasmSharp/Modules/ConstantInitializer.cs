using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 定数式の検証と検証済み式の評価を行う
/// </summary>
internal static class ConstantInitializer
{
    /// <summary>
    /// 定数式が許可された命令で期待する型の値を1個生成することを検証する
    /// </summary>
    /// <param name="expression">終端のendを含むデコード済みの定数式</param>
    /// <param name="expectedKind">式が生成する値に要求する型</param>
    /// <param name="importedGlobals">global添字順に並ぶimportしたglobalの型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    internal static void Validate(
        ImmutableArray<DecodedInstruction> expression,
        WasmValueKind expectedKind,
        ReadOnlySpan<WasmGlobalType> importedGlobals,
        byte sectionId
    )
    {
        WasmValueKind? resultKind = null;
        // 結果の型・個数より先に、左から最初の適格性違反を選ぶ。
        for (var index = 0; index < expression.Length - 1; index++)
        {
            resultKind = ValidateConstantInstruction(expression[index], importedGlobals, sectionId);
        }

        var endLocation = new WasmFailureLocation(
            WasmProcessingStage.Validate,
            expression[^1].ByteOffset,
            null,
            sectionId
        );
        if (expression.Length != 2)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                "global初期化式の結果が1個ではありません。",
                endLocation
            );
        }
        if (resultKind != expectedKind)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                "global初期化式の結果型が宣言と一致しません。",
                endLocation
            );
        }
    }

    /// <summary>
    /// 検証済みの単一定数またはglobal.getの式を評価する
    /// </summary>
    /// <param name="expression">単一の定数命令またはglobal.getとendからなる検証済みの式</param>
    /// <param name="globals">global添字順に並ぶ構築済みの実体</param>
    /// <returns>定数の即値または参照したglobalの現在値</returns>
    internal static WasmValue Evaluate(
        ImmutableArray<DecodedInstruction> expression,
        ReadOnlySpan<WasmGlobal> globals
    )
    {
        var initializer = expression[0];
        return initializer.Opcode == new OpcodeKey(0, 0x23)
            ? globals[(int)initializer.Index].Value
            : initializer.Immediate;
    }

    /// <summary>
    /// 定数式の命令の適格性を確認し、生成する値の型を返す
    /// </summary>
    /// <param name="instruction">end以外の初期化命令</param>
    /// <param name="importedGlobals">定数式から参照できるimportしたglobalの型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    /// <returns>命令が生成する値の型</returns>
    private static WasmValueKind ValidateConstantInstruction(
        DecodedInstruction instruction,
        ReadOnlySpan<WasmGlobalType> importedGlobals,
        byte sectionId
    )
    {
        var location = new WasmFailureLocation(
            WasmProcessingStage.Validate,
            instruction.ByteOffset,
            null,
            sectionId
        );
        if (instruction.Opcode == new OpcodeKey(0, 0x23))
        {
            if (instruction.Index >= (uint)importedGlobals.Length)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "global",
                    instruction.Index,
                    "global初期化式が参照するimport globalが存在しません。",
                    location
                );
            }
            var type = importedGlobals[(int)instruction.Index];
            if (type.IsMutable)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "constant expression required",
                    "global初期化式はimmutable globalだけを参照できます。",
                    location
                );
            }
            return type.ValueKind;
        }
        if (
            InstructionSet.TryGet(instruction.Opcode, out var descriptor)
            && descriptor.Validation == ValidationRule.Constant
        )
        {
            return instruction.Immediate.Kind;
        }
        throw ValidationDiagnostics.ValidationFailure(
            "constant expression required",
            "global初期化式に使用できない命令です。",
            location
        );
    }
}
