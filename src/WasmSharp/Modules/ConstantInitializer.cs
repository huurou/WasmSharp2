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
    /// <param name="references">当該module検証で共有する関数の個数と宣言集合</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    internal static void Validate(
        ImmutableArray<DecodedInstruction> expression,
        WasmValueKind expectedKind,
        ReadOnlySpan<WasmGlobalType> importedGlobals,
        ReferenceValidationContext references,
        byte sectionId
    )
    {
        // 結果の型・個数より先に、左から最初の適格性違反を選ぶ。
        for (var index = 0; index < expression.Length - 1; index++)
        {
            ValidateConstantInstruction(expression[index], importedGlobals, sectionId);
        }

        WasmValueKind? resultKind = null;
        for (var index = 0; index < expression.Length - 1; index++)
        {
            var instruction = expression[index];
            resultKind = ValidateResultInstruction(
                instruction,
                importedGlobals,
                references,
                sectionId
            );
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
                "定数式の結果が1個ではありません。",
                endLocation
            );
        }
        if (resultKind != expectedKind)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                "定数式の結果型が要求された型と一致しません。",
                endLocation
            );
        }
    }

    /// <summary>
    /// 検証済みの単一定数、関数参照またはglobal.getの式を評価する
    /// </summary>
    /// <param name="expression">単一の初期化命令とendからなる検証済みの式</param>
    /// <param name="globals">global添字順に並ぶ構築済みの実体</param>
    /// <param name="functions">関数添字順に並ぶ構築済みの実体</param>
    /// <returns>定数の即値、構築済みの同じ関数への参照、またはglobalの現在値</returns>
    internal static WasmValue Evaluate(
        ImmutableArray<DecodedInstruction> expression,
        ReadOnlySpan<WasmGlobal> globals,
        ReadOnlySpan<WasmFunction> functions
    )
    {
        var initializer = expression[0];
        if (initializer.Opcode == new OpcodeKey(0, 0xD2))
        {
            return WasmValue.FromFuncRef(functions[(int)initializer.Index]);
        }
        return initializer.Opcode == new OpcodeKey(0, 0x23)
            ? globals[(int)initializer.Index].Value
            : initializer.Immediate;
    }

    /// <summary>
    /// 定数式の命令の適格性を確認する
    /// </summary>
    /// <param name="instruction">end以外の初期化命令</param>
    /// <param name="importedGlobals">定数式から参照できるimportしたglobalの型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    private static void ValidateConstantInstruction(
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
                    "定数式が参照するimport globalが存在しません。",
                    location
                );
            }
            var type = importedGlobals[(int)instruction.Index];
            if (type.IsMutable)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "constant expression required",
                    "定数式はimmutable globalだけを参照できます。",
                    location
                );
            }
            return;
        }
        if (
            InstructionSet.TryGet(instruction.Opcode, out var descriptor)
            && descriptor.Validation is ValidationRule.Constant or ValidationRule.RefFunc
        )
        {
            return;
        }
        throw ValidationDiagnostics.ValidationFailure(
            "constant expression required",
            "定数式に使用できない命令です。",
            location
        );
    }

    /// <summary>
    /// 定数性の検査後に参照の適格性を確認し、命令の結果型を返す
    /// </summary>
    /// <param name="instruction">定数性を確認した初期化命令</param>
    /// <param name="importedGlobals">定数式から参照できるimportしたglobalの型</param>
    /// <param name="references">当該module検証で共有する関数の個数と宣言集合</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    /// <returns>命令が生成する値の型</returns>
    private static WasmValueKind ValidateResultInstruction(
        DecodedInstruction instruction,
        ReadOnlySpan<WasmGlobalType> importedGlobals,
        ReferenceValidationContext references,
        byte sectionId
    )
    {
        if (instruction.Opcode == new OpcodeKey(0, 0x23))
        {
            return importedGlobals[(int)instruction.Index].ValueKind;
        }
        var location = new WasmFailureLocation(
            WasmProcessingStage.Validate,
            instruction.ByteOffset,
            null,
            sectionId
        );
        if (instruction.Opcode == new OpcodeKey(0, 0xD2))
        {
            if (instruction.Index >= references.FunctionCount)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "function",
                    instruction.Index,
                    "定数式が参照する関数が存在しません。",
                    location
                );
            }
            if (!references.DeclaredFunctions.Contains(instruction.Index))
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "undeclared function reference",
                    "定数式が参照する関数が宣言されていません。",
                    location
                );
            }
            return WasmValueKind.FuncRef;
        }
        if (
            instruction.Opcode == new OpcodeKey(0, 0xD0)
            && instruction.Immediate.Kind is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef)
        )
        {
            throw ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                "ref.nullには参照型が必要です。",
                location
            );
        }
        return instruction.Immediate.Kind;
    }
}
