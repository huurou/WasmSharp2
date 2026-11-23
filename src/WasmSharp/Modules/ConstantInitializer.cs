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
    /// <param name="expectedType">式が生成する値に要求する型</param>
    /// <param name="visibleGlobals">この式から参照できる、global添字順の型</param>
    /// <param name="functionTypes">importを先頭にした関数添字順の型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    /// <param name="specVersion">定数式に適用する仕様の版</param>
    internal static void Validate(
        ImmutableArray<DecodedInstruction> expression,
        WasmValueType expectedType,
        ReadOnlySpan<WasmGlobalType> visibleGlobals,
        ReadOnlySpan<WasmFunctionType> functionTypes,
        byte sectionId,
        WasmSpecVersion specVersion
    )
    {
        // 結果の型・個数より先に、左から最初の適格性違反を選ぶ。
        for (var index = 0; index < expression.Length - 1; index++)
        {
            ValidateConstantInstruction(expression[index], visibleGlobals, sectionId, specVersion);
        }

        var operands =
            expression.Length <= 32 ? stackalloc WasmValueKind[expression.Length] : new WasmValueKind[expression.Length];
        var count = 0;
        // 数値演算の入力は種類だけで判定できる。各命令が1個積むため、結果が1個なら最後の命令がその型を決める。
        var resultType = default(WasmValueType);
        var instructionSet = InstructionSet.ForVersion(specVersion);
        for (var index = 0; index < expression.Length - 1; index++)
        {
            var instruction = expression[index];
            if (
                instructionSet.TryGetValue(instruction.Opcode, out var descriptor)
                && descriptor.NumericSignature is { } signature
            )
            {
                for (var input = signature.InputTypes.Length - 1; input >= 0; input--)
                {
                    if (count == 0 || operands[--count] != signature.InputTypes[input])
                    {
                        throw ValidationDiagnostics.ValidationFailure(
                            "type mismatch",
                            "定数式の演算に必要な型の値がありません。",
                            new WasmFailureLocation(WasmProcessingStage.Validate, instruction.ByteOffset, null, sectionId)
                        );
                    }
                }
                resultType = signature.ResultType;
            }
            else
            {
                resultType = ValidateResultInstruction(instruction, visibleGlobals, functionTypes, sectionId, specVersion);
            }
            operands[count++] = resultType.Kind;
        }

        var endLocation = new WasmFailureLocation(WasmProcessingStage.Validate, expression[^1].ByteOffset, null, sectionId);
        if (count != 1)
        {
            throw ValidationDiagnostics.ValidationFailure("type mismatch", "定数式の結果が1個ではありません。", endLocation);
        }
        if (!expectedType.Accepts(resultType))
        {
            throw ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                "定数式の結果型が要求された型と一致しません。",
                endLocation
            );
        }
    }

    /// <summary>
    /// 検証済みの定数式を評価する
    /// </summary>
    /// <param name="expression">終端のendを含む検証済みの式</param>
    /// <param name="globals">global添字順に並ぶ構築済みの実体</param>
    /// <param name="functions">関数添字順に並ぶ構築済みの実体</param>
    /// <param name="specVersion">定数式を検証した仕様の版</param>
    /// <returns>式の実行後にスタックに残る値</returns>
    internal static WasmValue Evaluate(
        ImmutableArray<DecodedInstruction> expression,
        ReadOnlySpan<WasmGlobal> globals,
        ReadOnlySpan<WasmFunction> functions,
        WasmSpecVersion specVersion
    )
    {
        if (expression.Length == 2)
        {
            return ReadValue(expression[0], globals, functions);
        }

        var operands = new Stack<WasmValue>();
        var instructionSet = InstructionSet.ForVersion(specVersion);
        for (var index = 0; index < expression.Length - 1; index++)
        {
            var instruction = expression[index];
            if (
                instructionSet.TryGetValue(instruction.Opcode, out var descriptor)
                && descriptor.ConstantBinaryOperation is { } operation
            )
            {
                var right = operands.Pop();
                operands.Push(operation(operands.Pop(), right));
            }
            else
            {
                operands.Push(ReadValue(instruction, globals, functions));
            }
        }
        return operands.Pop();
    }

    /// <summary>
    /// 定数命令が積む即値または構築済みの外部値を取得する
    /// </summary>
    private static WasmValue ReadValue(
        DecodedInstruction initializer,
        ReadOnlySpan<WasmGlobal> globals,
        ReadOnlySpan<WasmFunction> functions
    )
    {
        if (initializer.Opcode == new OpcodeKey(0, 0xD2))
        {
            return WasmValue.FromFuncRef(functions[(int)initializer.Index]);
        }
        return initializer.Opcode == new OpcodeKey(0, 0x23) ? globals[(int)initializer.Index].Value : initializer.Immediate;
    }

    /// <summary>
    /// 定数式の命令の適格性を確認する
    /// </summary>
    /// <param name="instruction">end以外の初期化命令</param>
    /// <param name="visibleGlobals">この式から参照できるglobalの型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    /// <param name="specVersion">定数式に適用する仕様の版</param>
    private static void ValidateConstantInstruction(
        DecodedInstruction instruction,
        ReadOnlySpan<WasmGlobalType> visibleGlobals,
        byte sectionId,
        WasmSpecVersion specVersion
    )
    {
        var location = new WasmFailureLocation(WasmProcessingStage.Validate, instruction.ByteOffset, null, sectionId);
        if (instruction.Opcode == new OpcodeKey(0, 0x23))
        {
            if (instruction.Index >= (uint)visibleGlobals.Length)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "global",
                    instruction.Index,
                    specVersion == WasmSpecVersion.Core2
                        ? "定数式が参照するimport globalが存在しません。"
                        : "定数式から参照できる範囲にglobalが存在しません。",
                    location
                );
            }
            var type = visibleGlobals[(int)instruction.Index];
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
            InstructionSet.TryGet(instruction.Opcode, specVersion, out var descriptor)
            && (
                descriptor.Validation is ValidationRule.Constant or ValidationRule.RefFunc
                || (specVersion == WasmSpecVersion.Core3 && descriptor.ConstantBinaryOperation is not null)
            )
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
    /// <param name="visibleGlobals">この式から参照できるglobalの型</param>
    /// <param name="functionTypes">importを先頭にした関数添字順の型</param>
    /// <param name="sectionId">診断位置に保持するセクションID</param>
    /// <param name="specVersion">定数式に適用する仕様の版</param>
    /// <returns>命令が生成する値の型</returns>
    private static WasmValueType ValidateResultInstruction(
        DecodedInstruction instruction,
        ReadOnlySpan<WasmGlobalType> visibleGlobals,
        ReadOnlySpan<WasmFunctionType> functionTypes,
        byte sectionId,
        WasmSpecVersion specVersion
    )
    {
        if (instruction.Opcode == new OpcodeKey(0, 0x23))
        {
            return visibleGlobals[(int)instruction.Index].ValueType;
        }
        if (instruction.Opcode == new OpcodeKey(0, 0xD2))
        {
            if (instruction.Index >= (uint)functionTypes.Length)
            {
                var location = new WasmFailureLocation(
                    WasmProcessingStage.Validate,
                    instruction.ByteOffset,
                    null,
                    sectionId
                );
                throw ValidationDiagnostics.UnknownIndex(
                    "function",
                    instruction.Index,
                    "定数式が参照する関数が存在しません。",
                    location
                );
            }
            // 定数式のref.func自体が関数の宣言に数えられる。
            return specVersion == WasmSpecVersion.Core3
                ? WasmValueType.FunctionReference(functionTypes[(int)instruction.Index], false)
                : WasmValueKind.FuncRef;
        }
        if (instruction.Opcode == new OpcodeKey(0, 0xD0))
        {
            return instruction.ReferenceType.Validate();
        }
        return instruction.Immediate.Kind;
    }
}
