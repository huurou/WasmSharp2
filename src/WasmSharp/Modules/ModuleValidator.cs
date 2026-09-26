using System.Collections.Immutable;
using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules.Imports;

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
    /// <returns>importを含まない定義順の線形実行コード</returns>
    internal static ImmutableArray<FunctionCode> Validate(WasmModule module)
    {
        ValidateReferences(module);
        ValidateInitializers(module);
        ValidateStart(module);
        var importedFunctionCount = (uint)module.Imports.Count(x => x is FunctionImport);
        var codes = ImmutableArray.CreateBuilder<FunctionCode>(module.Functions.Length);
        for (var definitionIndex = 0; definitionIndex < module.Functions.Length; definitionIndex++)
        {
            codes.Add(
                ValidateFunction(
                    module,
                    definitionIndex,
                    importedFunctionCount + (uint)definitionIndex
                )
            );
        }

        return codes.MoveToImmutable();
    }

    /// <summary>
    /// globalの初期化式を評価せず、1個の宣言型の値を生成することを検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateInitializers(WasmModule module)
    {
        var importedGlobals = module.Imports.OfType<GlobalImport>().ToArray();
        foreach (var global in module.Globals)
        {
            var endLocation = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                global.Initializer[^1].ByteOffset,
                null,
                6
            );
            // 対応する初期化命令はいずれも値を1個積むため、命令1個とendだけを許す。
            if (global.Initializer.Length != 2)
            {
                throw new WasmValidateException(
                    "global初期化式の結果が1個ではありません。",
                    endLocation,
                    null
                );
            }

            var instruction = global.Initializer[0];
            var location = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                instruction.ByteOffset,
                null,
                6
            );
            WasmValueKind resultKind;
            if (instruction.Opcode == new OpcodeKey(0, 0x23))
            {
                if (instruction.Index >= (uint)importedGlobals.Length)
                {
                    throw new WasmValidateException(
                        "global初期化式はimportしたglobalだけを参照できます。",
                        location,
                        null
                    );
                }
                var type = importedGlobals[(int)instruction.Index].Type;
                if (type.IsMutable)
                {
                    throw new WasmValidateException(
                        "global初期化式はimmutable globalだけを参照できます。",
                        location,
                        null
                    );
                }
                resultKind = type.ValueKind;
            }
            else if (
                InstructionSet.TryGet(instruction.Opcode, out var descriptor)
                && descriptor.Validation == ValidationRule.Constant
            )
            {
                resultKind = instruction.Immediate.Kind;
            }
            else
            {
                throw new WasmValidateException(
                    "global初期化式に使用できない命令です。",
                    location,
                    null
                );
            }
            if (resultKind != global.Type.ValueKind)
            {
                throw new WasmValidateException(
                    "global初期化式の結果型が宣言と一致しません。",
                    endLocation,
                    null
                );
            }
        }
    }

    /// <summary>
    /// importと定義の関数添字空間でstartの型が引数・結果とも0個であることを検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateStart(WasmModule module)
    {
        if (module.Start is not { } start)
        {
            return;
        }

        var imports = module.Imports.OfType<FunctionImport>().ToArray();
        var location = new WasmFailureLocation(
            WasmProcessingStage.Validate,
            start.ByteOffset,
            start.FunctionIndex,
            8
        );
        if (start.FunctionIndex >= (ulong)imports.Length + (uint)module.Functions.Length)
        {
            throw new WasmValidateException("startが参照する関数が存在しません。", location, null);
        }

        var typeIndex =
            start.FunctionIndex < (uint)imports.Length
                ? imports[(int)start.FunctionIndex].TypeIndex
                : module.Functions[(int)(start.FunctionIndex - (uint)imports.Length)].TypeIndex;
        var type = module.Types[(int)typeIndex];
        if (!type.Parameters.IsEmpty || !type.Results.IsEmpty)
        {
            throw new WasmValidateException(
                "startの関数型は引数・結果とも0個である必要があります。",
                location,
                null
            );
        }
    }

    private static FunctionCode ValidateFunction(
        WasmModule module,
        int definitionIndex,
        uint moduleFunctionIndex
    )
    {
        var function = module.Functions[definitionIndex];
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
                            new WasmFailureLocation(
                                WasmProcessingStage.Validate,
                                instruction.ByteOffset,
                                moduleFunctionIndex,
                                10
                            ),
                            null
                        );
                    }
                    break;

                // 実行handlerを先に接続した命令は、型検査を実装するまで検証段階の未実装として止める。
                case ValidationRule.Unreachable:
                case ValidationRule.Call:
                case ValidationRule.Return:
                case ValidationRule.Drop:
                case ValidationRule.LocalGet:
                case ValidationRule.LocalSet:
                case ValidationRule.LocalTee:
                case ValidationRule.GlobalGet:
                case ValidationRule.GlobalSet:
                    throw new WasmUnsupportedFeatureException(
                        "未実装の命令検証に遭遇しました。",
                        descriptor.Name,
                        new WasmFailureLocation(
                            WasmProcessingStage.Validate,
                            instruction.ByteOffset,
                            moduleFunctionIndex,
                            10
                        ),
                        [
                            new WasmUnverifiedRange(
                                WasmProcessingStage.Validate,
                                instruction.ByteOffset,
                                module.InputLength,
                                "この命令以降の検証が未完了です。"
                            ),
                        ]
                    );

                default:
                    throw new InvalidOperationException("デコード済み命令の検証規則が不正です。");
            }

            instructions.Add(
                new Instruction(
                    descriptor.ExecutionOpcode!.Value,
                    instruction.Immediate,
                    instruction.ByteOffset,
                    instruction.Index
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
                new WasmFailureLocation(
                    WasmProcessingStage.Validate,
                    function.BodyOffset,
                    moduleFunctionIndex,
                    10
                ),
                [
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Validate,
                        function.BodyOffset,
                        module.InputLength,
                        "この関数以降の検証が未完了です。"
                    ),
                ]
            );
        }

        return new FunctionCode(
            instructions.MoveToImmutable(),
            function.Locals.AsSpan(),
            maxOperandStack
        );
    }

    /// <summary>
    /// importを先頭とする種類別の添字空間とリソース宣言を検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateReferences(WasmModule module)
    {
        uint functionCount = 0;
        uint tableCount = 0;
        uint memoryCount = 0;
        uint globalCount = 0;
        foreach (var import in module.Imports)
        {
            var location = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                import.ByteOffset,
                null,
                2
            );
            switch (import)
            {
                case FunctionImport function:
                    ValidateTypeIndex(
                        module,
                        function.TypeIndex,
                        location with
                        {
                            FunctionIndex = functionCount,
                        }
                    );
                    functionCount++;
                    break;

                case TableImport table:
                    ValidateLimits(table.Type.Limits, uint.MaxValue, location);
                    tableCount++;
                    break;

                case MemoryImport memory:
                    ValidateMemory(memory.Type.Limits, ++memoryCount, location);
                    break;

                case GlobalImport:
                    globalCount++;
                    break;
            }
        }

        foreach (var function in module.Functions)
        {
            ValidateTypeIndex(
                module,
                function.TypeIndex,
                new(WasmProcessingStage.Validate, function.BodyOffset, functionCount++, 10)
            );
        }
        foreach (var table in module.Tables)
        {
            ValidateLimits(
                table.Limits,
                uint.MaxValue,
                new(WasmProcessingStage.Validate, table.ByteOffset, null, 4)
            );
            tableCount++;
        }
        foreach (var memory in module.Memories)
        {
            ValidateMemory(
                memory.Limits,
                ++memoryCount,
                new(WasmProcessingStage.Validate, memory.ByteOffset, null, 5)
            );
        }
        globalCount += (uint)module.Globals.Length;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var export in module.Exports)
        {
            var count = export.Kind switch
            {
                WasmExternalKind.Function => functionCount,
                WasmExternalKind.Table => tableCount,
                WasmExternalKind.Memory => memoryCount,
                WasmExternalKind.Global => globalCount,
                _ => throw new InvalidOperationException("デコード済みexportの種類が不正です。"),
            };
            var location = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                export.ByteOffset,
                export.Kind == WasmExternalKind.Function ? export.Index : null,
                7
            );
            if (export.Index >= count)
            {
                throw new WasmValidateException(
                    "exportが参照する外部要素が存在しません。",
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

    /// <summary>
    /// 縮小変換する前に関数型の存在を検証する
    /// </summary>
    /// <param name="module">関数型を保持するmodule</param>
    /// <param name="typeIndex">未検証の型index</param>
    /// <param name="location">参照元の位置</param>
    private static void ValidateTypeIndex(
        WasmModule module,
        uint typeIndex,
        WasmFailureLocation location
    )
    {
        if (typeIndex >= (uint)module.Types.Length)
        {
            throw new WasmValidateException("参照する関数型が存在しません。", location, null);
        }
    }

    /// <summary>
    /// memoryの個数制約とページ数のlimitsを検証する
    /// </summary>
    /// <param name="limits">未検証のページ数の範囲</param>
    /// <param name="count">importと定義の累計</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateMemory(WasmLimits limits, uint count, WasmFailureLocation location)
    {
        if (count > 1)
        {
            throw new WasmValidateException(
                "memoryのimportと定義の合計が1個を超えています。",
                location,
                null
            );
        }
        ValidateLimits(limits, 65536, location);
    }

    /// <summary>
    /// 実割当や実装保持上限とは分けてlimitsの仕様上の制約を検証する
    /// </summary>
    /// <param name="limits">未検証の範囲</param>
    /// <param name="maximum">仕様上の最大値</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateLimits(
        WasmLimits limits,
        uint maximum,
        WasmFailureLocation location
    )
    {
        if (
            limits.Minimum > maximum
            || limits.Maximum is { } max && (max > maximum || limits.Minimum > max)
        )
        {
            throw new WasmValidateException(
                "limitsが仕様上の範囲を満たしていません。",
                location,
                null
            );
        }
    }
}
