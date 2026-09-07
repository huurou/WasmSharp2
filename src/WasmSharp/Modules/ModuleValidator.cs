using System.Collections.Immutable;
using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 静的定義の検証と線形実行コードの生成を行う
/// </summary>
internal static class ModuleValidator
{
    /// <summary>
    /// 全体の参照と各関数を検証し、完成した実行コードを返す
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <returns>関数index順の線形実行コード</returns>
    internal static ImmutableArray<FunctionCode> Validate(WasmModule module)
    {
        ValidateReferences(module);
        var codes = ImmutableArray.CreateBuilder<FunctionCode>(module.Functions.Length);
        for (var index = 0; index < module.Functions.Length; index++)
        {
            codes.Add(ValidateFunction(module, index));
        }

        return codes.MoveToImmutable();
    }

    private static FunctionCode ValidateFunction(WasmModule module, int index)
    {
        var function = module.Functions[index];
        var type = module.Types[(int)function.TypeIndex];
        var instructions = ImmutableArray.CreateBuilder<Instruction>(function.Instructions.Length);
        List<WasmValueKind> stack = [];
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
                    maxOperandStack = Math.Max(maxOperandStack, stack.Count);
                    break;

                case ValidationRule.FunctionEnd:
                    if (!CollectionsMarshal.AsSpan(stack).SequenceEqual(type.Results.AsSpan()))
                    {
                        throw new WasmValidateException(
                            "関数の結果の型・個数・順序が宣言と一致しません。",
                            new(
                                WasmProcessingStage.Validate,
                                instruction.ByteOffset,
                                (uint)index,
                                10
                            ),
                            null
                        );
                    }
                    break;

                default:
                    throw new InvalidOperationException("デコード済み命令の検証規則が不正です。");
            }

            instructions.Add(
                new(
                    descriptor.ExecutionOpcode!.Value,
                    instruction.Immediate,
                    instruction.ByteOffset
                )
            );
        }

        // endで型・個数を検査してから実行形を判定する。結果1個なら定数pushも1個になる。
        string? feature = null;
        if (!type.Parameters.IsEmpty)
        {
            feature = "function.parameters";
        }
        else if (function.Locals.Any(x => x.Count != 0))
        {
            feature = "function.locals";
        }
        else if (type.Results.Length != 1)
        {
            feature = "function.results";
        }

        if (feature != null)
        {
            throw new WasmUnsupportedFeatureException(
                "未実装の関数実行形に遭遇しました。",
                feature,
                new(WasmProcessingStage.Validate, function.BodyOffset, (uint)index, 10),
                [
                    new(
                        WasmProcessingStage.Validate,
                        function.BodyOffset,
                        module.InputLength,
                        "この関数以降の検証が未完了です。"
                    ),
                ]
            );
        }

        return new(instructions.MoveToImmutable(), maxOperandStack);
    }

    /// <summary>
    /// 関数本体より先に全体の添字とexport名を検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateReferences(WasmModule module)
    {
        for (var index = 0; index < module.Functions.Length; index++)
        {
            var function = module.Functions[index];
            // 存在をu32のまま確認すれば、後段のint変換も保持済み配列の範囲内になる。
            if (function.TypeIndex >= (uint)module.Types.Length)
            {
                throw new WasmValidateException(
                    "参照する関数型が存在しません。",
                    new(WasmProcessingStage.Validate, function.BodyOffset, (uint)index, 10),
                    null
                );
            }
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (var export in module.Exports)
        {
            var location = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                export.ByteOffset,
                export.FunctionIndex,
                7
            );
            if (export.FunctionIndex >= (uint)module.Functions.Length)
            {
                throw new WasmValidateException(
                    "exportが参照する関数が存在しません。",
                    location,
                    null
                );
            }

            if (!names.Add(export.Name))
            {
                throw new WasmValidateException("export名が重複しています。", location, null);
            }
        }
    }
}
