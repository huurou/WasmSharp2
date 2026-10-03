using System.Collections.Immutable;
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
        var (functionCount, tableCount, memoryCount, globalCount) = ValidateImportReferences(
            module
        );
        ValidateFunctionReferences(module, functionCount);
        ValidateResources(module, memoryCount);
        ValidateExports(
            module,
            functionCount + (uint)module.Functions.Length,
            tableCount + (uint)module.Tables.Length,
            memoryCount + (uint)module.Memories.Length,
            globalCount + (uint)module.Globals.Length
        );
        ValidateInitializers(module);
        ValidateStart(module);
        return ValidateFunctions(module);
    }

    /// <summary>
    /// 各定義関数を1回の型検査と線形化で処理し、全成功時だけコードを返す
    /// </summary>
    /// <param name="module">参照・リソース・初期化式・startの検証を終えたmodule</param>
    /// <returns>ローカルに保持した定義順の線形実行コード</returns>
    private static ImmutableArray<FunctionCode> ValidateFunctions(WasmModule module)
    {
        var importedFunctionCount = (uint)module.Imports.Count(x => x is FunctionImport);
        WasmFunctionType[] functionTypes =
        [
            .. module.Imports.OfType<FunctionImport>().Select(x => module.Types[(int)x.TypeIndex]),
            .. module.Functions.Select(x => module.Types[(int)x.TypeIndex]),
        ];
        WasmGlobalType[] globalTypes =
        [
            .. module.Imports.OfType<GlobalImport>().Select(x => x.Type),
            .. module.Globals.Select(x => x.Type),
        ];
        var codes = ImmutableArray.CreateBuilder<FunctionCode>(module.Functions.Length);
        for (var definitionIndex = 0; definitionIndex < module.Functions.Length; definitionIndex++)
        {
            codes.Add(
                ValidateFunction(
                    module,
                    definitionIndex,
                    importedFunctionCount + (uint)definitionIndex,
                    functionTypes,
                    globalTypes
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

    /// <summary>
    /// 定義関数の参照と値スタックの型を検証し、対応する線形実行コードを作成する
    /// </summary>
    /// <param name="module">全体の参照、リソース宣言、global初期化式とstartの検証を終えたmodule</param>
    /// <param name="definitionIndex">importを含まない定義順の関数index</param>
    /// <param name="moduleFunctionIndex">診断に使用する、importを含むmodule全体の関数index</param>
    /// <param name="functionTypes">importを先頭とする関数index順の型</param>
    /// <param name="globalTypes">importを先頭とするglobal index順の型</param>
    /// <returns>実行命令、追加localsとoperandの最大要素数を持つ、この定義関数の実行コード</returns>
    /// <exception cref="WasmValidateException">命令の参照先、globalの可変性、入力型または結果の型・個数・順序が不正な場合</exception>
    /// <exception cref="InvalidOperationException">デコード済み命令の記述子や検証規則が実装内で一致しない場合</exception>
    private static FunctionCode ValidateFunction(
        WasmModule module,
        int definitionIndex,
        uint moduleFunctionIndex,
        WasmFunctionType[] functionTypes,
        WasmGlobalType[] globalTypes
    )
    {
        const int STACKALLOC_DECLARATION_LIMIT = 128;

        var function = module.Functions[definitionIndex];
        var type = module.Types[(int)function.TypeIndex];
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
                        throw CreateException(
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
                        throw CreateException(
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
                            throw CreateException(
                                "immutable globalは更新できません。",
                                instruction.ByteOffset
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

            throw CreateException("参照するlocalが存在しません。", byteOffset);
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
            return new WasmValidateException(
                message,
                new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10),
                null
            );
        }
    }

    /// <summary>
    /// importの型参照とリソース制約を宣言順に検証し、種類別の個数を返す
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <returns>importした関数・table・memory・globalの個数</returns>
    private static (
        uint FunctionCount,
        uint TableCount,
        uint MemoryCount,
        uint GlobalCount
    ) ValidateImportReferences(WasmModule module)
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

        return (functionCount, tableCount, memoryCount, globalCount);
    }

    /// <summary>
    /// 定義関数の型参照を宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="functionCount">importした関数の個数</param>
    private static void ValidateFunctionReferences(WasmModule module, uint functionCount)
    {
        foreach (var function in module.Functions)
        {
            ValidateTypeIndex(
                module,
                function.TypeIndex,
                new(WasmProcessingStage.Validate, function.BodyOffset, functionCount++, 10)
            );
        }
    }

    /// <summary>
    /// 定義tableのlimitsと、定義memoryの個数・limitsを宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="memoryCount">importしたmemoryの個数</param>
    private static void ValidateResources(WasmModule module, uint memoryCount)
    {
        foreach (var table in module.Tables)
        {
            ValidateLimits(
                table.Limits,
                uint.MaxValue,
                new(WasmProcessingStage.Validate, table.ByteOffset, null, 4)
            );
        }
        foreach (var memory in module.Memories)
        {
            ValidateMemory(
                memory.Limits,
                ++memoryCount,
                new(WasmProcessingStage.Validate, memory.ByteOffset, null, 5)
            );
        }
    }

    /// <summary>
    /// exportの参照先と名前の一意性を宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="functionCount">importと定義を合わせた関数の個数</param>
    /// <param name="tableCount">importと定義を合わせたtableの個数</param>
    /// <param name="memoryCount">importと定義を合わせたmemoryの個数</param>
    /// <param name="globalCount">importと定義を合わせたglobalの個数</param>
    private static void ValidateExports(
        WasmModule module,
        uint functionCount,
        uint tableCount,
        uint memoryCount,
        uint globalCount
    )
    {
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
    /// リソースの割り当てや実装の保持上限とは区別して、limitsの仕様上の制約を検証する
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
