using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 1関数の型検査と線形実行コードの生成を行う
/// </summary>
internal static class FunctionValidator
{
    /// <summary>
    /// 定義関数の参照と値スタックの型を検証し、対応する線形実行コードを作成する
    /// </summary>
    /// <remarks>入力の型配列とmoduleの検証成功状態・実行コードは変更しない</remarks>
    /// <param name="module">importと定義関数の型参照、global初期化式、リソース宣言の検証を終えたmodule</param>
    /// <param name="definitionIndex">importを含まない定義順の関数index</param>
    /// <param name="moduleFunctionIndex">型の参照と診断に使用する、importを含むmodule全体の関数index</param>
    /// <param name="functionTypes">importを先頭とする関数index順の解決済みの型</param>
    /// <param name="globalTypes">importを先頭とするglobal index順の解決済みの型</param>
    /// <returns>実行命令、追加localsとoperandの最大要素数を持つ、この定義関数の実行コード</returns>
    /// <exception cref="WasmValidateException">命令の参照先、globalの可変性、入力型または結果の型・個数・順序が不正な場合</exception>
    /// <exception cref="InvalidOperationException">デコード済み命令の記述子や検証規則が実装内で一致しない場合</exception>
    internal static FunctionCode Validate(
        WasmModule module,
        int definitionIndex,
        uint moduleFunctionIndex,
        WasmFunctionType[] functionTypes,
        WasmGlobalType[] globalTypes
    )
    {
        const int STACKALLOC_DECLARATION_LIMIT = 128;

        var function = module.Functions[definitionIndex];
        var type = functionTypes[(int)moduleFunctionIndex];
        // 圧縮宣言ごとの累積終端を使い、localの個数に比例する配列は作らない。
        var localEnds =
            function.Locals.Length <= STACKALLOC_DECLARATION_LIMIT
                ? stackalloc ulong[function.Locals.Length]
                : new ulong[function.Locals.Length];
        ulong localCount = 0;
        for (var index = 0; index < function.Locals.Length; index++)
        {
            localCount += function.Locals[index].Count;
            localEnds[index] = localCount;
        }
        var instructions = ImmutableArray.CreateBuilder<Instruction>(function.Instructions.Length);
        List<WasmValueKind> stack = [];
        var unreachable = false;
        var maxOperandStack = 0;
        foreach (var instruction in function.Instructions)
        {
            if (!InstructionSet.TryGet(instruction.Opcode, out var descriptor))
            {
                throw new InvalidOperationException("デコード済み命令の情報がありません。");
            }

            switch (descriptor.Validation)
            {
                case ValidationRule.Constant:
                    stack.Add(
                        descriptor.StackEffect switch
                        {
                            StackEffectKind.PushI32 => WasmValueKind.I32,
                            StackEffectKind.PushI64 => WasmValueKind.I64,
                            StackEffectKind.PushF32 => WasmValueKind.F32,
                            StackEffectKind.PushF64 => WasmValueKind.F64,
                            _ => throw new InvalidOperationException(
                                "定数命令のスタック効果が不正です。"
                            ),
                        }
                    );
                    break;

                case ValidationRule.Numeric:
                    var numericSignature =
                        descriptor.NumericSignature
                        ?? throw new InvalidOperationException("数値命令の型情報がありません。");
                    for (var index = 0; index < numericSignature.InputCount; index++)
                    {
                        Pop(numericSignature.InputType, instruction.ByteOffset);
                    }
                    stack.Add(numericSignature.ResultType);
                    break;

                case ValidationRule.FunctionEnd:
                    PopTypes(type.Results.AsSpan(), instruction.ByteOffset);
                    if (stack.Count != 0)
                    {
                        throw CreateException(
                            "関数の結果の型・個数・順序が宣言と一致しません。",
                            instruction.ByteOffset
                        );
                    }
                    break;

                case ValidationRule.Call:
                    if (instruction.Index >= (uint)functionTypes.Length)
                    {
                        throw CreateUnknownIndex(
                            "function",
                            instruction.Index,
                            "参照する関数が存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    var calleeType = functionTypes[(int)instruction.Index];
                    PopTypes(calleeType.Parameters.AsSpan(), instruction.ByteOffset);
                    stack.AddRange(calleeType.Results);
                    break;

                case ValidationRule.Return:
                    PopTypes(type.Results.AsSpan(), instruction.ByteOffset);
                    stack.Clear();
                    unreachable = true;
                    break;

                case ValidationRule.Unreachable:
                    stack.Clear();
                    unreachable = true;
                    break;

                case ValidationRule.GlobalGet:
                case ValidationRule.GlobalSet:
                    if (instruction.Index >= (uint)globalTypes.Length)
                    {
                        throw CreateUnknownIndex(
                            "global",
                            instruction.Index,
                            "参照するglobalが存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    var globalType = globalTypes[(int)instruction.Index];
                    if (descriptor.Validation == ValidationRule.GlobalGet)
                    {
                        stack.Add(globalType.ValueKind);
                    }
                    else
                    {
                        if (!globalType.IsMutable)
                        {
                            throw ValidationDiagnostics.ValidationFailure(
                                "global is immutable",
                                "immutable globalは更新できません。",
                                new(
                                    WasmProcessingStage.Validate,
                                    instruction.ByteOffset,
                                    moduleFunctionIndex,
                                    10
                                )
                            );
                        }
                        Pop(globalType.ValueKind, instruction.ByteOffset);
                    }
                    break;

                case ValidationRule.Drop:
                    Pop(null, instruction.ByteOffset);
                    break;

                case ValidationRule.LocalGet:
                case ValidationRule.LocalSet:
                case ValidationRule.LocalTee:
                    var localType = GetLocalType(
                        instruction.Index,
                        instruction.ByteOffset,
                        localEnds
                    );
                    if (descriptor.Validation != ValidationRule.LocalGet)
                    {
                        Pop(localType, instruction.ByteOffset);
                    }
                    if (descriptor.Validation != ValidationRule.LocalSet)
                    {
                        stack.Add(localType);
                    }
                    break;

                default:
                    throw new InvalidOperationException("デコード済み命令の検証規則が不正です。");
            }

            maxOperandStack = Math.Max(maxOperandStack, stack.Count);
            instructions.Add(
                new Instruction(
                    descriptor.ExecutionOpcode!.Value,
                    instruction.Immediate,
                    instruction.ByteOffset,
                    instruction.Index
                )
            );
        }

        return new FunctionCode(
            instructions.MoveToImmutable(),
            function.Locals.AsSpan(),
            maxOperandStack
        );

        WasmValueKind GetLocalType(uint index, long byteOffset, ReadOnlySpan<ulong> ends)
        {
            if (index < (uint)type.Parameters.Length)
            {
                return type.Parameters[(int)index];
            }
            var localIndex = index - (uint)type.Parameters.Length;
            var lower = 0;
            var upper = ends.Length;
            // 個数0の宣言を飛ばすため、添字より大きい最初の終端を探す。
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (localIndex < ends[middle])
                {
                    upper = middle;
                }
                else
                {
                    lower = middle + 1;
                }
            }

            if (lower < ends.Length)
            {
                return function.Locals[lower].Type;
            }

            throw CreateUnknownIndex("local", index, "参照するlocalが存在しません。", byteOffset);
        }

        void Pop(WasmValueKind? expected, long byteOffset)
        {
            if (stack.Count == 0)
            {
                // 到達不能な関数底だけがunknownを供給する。積まれた具体型は通常どおり検査する。
                if (unreachable)
                {
                    return;
                }

                throw CreateException("命令の入力値が不足しています。", byteOffset);
            }
            var actual = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            if (expected is { } kind && actual != kind)
            {
                throw CreateException("命令の入力型が一致しません。", byteOffset);
            }
        }

        // 引数と結果は宣言順に積まれるため、末尾から型を照合する。
        void PopTypes(ReadOnlySpan<WasmValueKind> kinds, long byteOffset)
        {
            for (var index = kinds.Length - 1; index >= 0; index--)
            {
                Pop(kinds[index], byteOffset);
            }
        }

        WasmValidateException CreateException(string message, long byteOffset)
        {
            return ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                message,
                new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10)
            );
        }

        WasmValidateException CreateUnknownIndex(
            string category,
            uint index,
            string message,
            long byteOffset
        )
        {
            return ValidationDiagnostics.UnknownIndex(
                category,
                index,
                message,
                new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10)
            );
        }
    }
}
