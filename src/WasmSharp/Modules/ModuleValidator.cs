using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
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
        var (functionCount, _, memoryCount, globalCount) = ValidateImportReferences(module);
        ValidateFunctionReferences(module, functionCount);
        var references = ReferenceValidationContext.Create(module, functionCount);
        ValidateInitializers(module, references);
        ValidateResources(module);
        ValidateData(module, memoryCount, references);
        var codes = ValidateFunctions(module);
        ValidateStart(module);
        ValidateExports(
            module,
            references.FunctionCount,
            (uint)references.TableElementTypes.Length,
            memoryCount + (uint)module.Memories.Length,
            globalCount + (uint)module.Globals.Length
        );
        ValidateMemoryCount(module, memoryCount);
        return codes;
    }

    /// <summary>
    /// アクティブデータセグメントのメモリ参照とi32のオフセット式を宣言順に検証する
    /// </summary>
    /// <param name="module">リソース宣言の検証を終えたmodule</param>
    /// <param name="importedMemoryCount">importしたmemoryの個数</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    private static void ValidateData(
        WasmModule module,
        uint importedMemoryCount,
        ReferenceValidationContext references
    )
    {
        var memoryCount = importedMemoryCount + (uint)module.Memories.Length;
        var importedGlobals = module.Imports.OfType<GlobalImport>().Select(x => x.Type).ToArray();
        foreach (var data in module.DataSegments)
        {
            if (data.ActiveTarget is not { } target)
            {
                continue;
            }
            if (target.MemoryIndex >= memoryCount)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "memory",
                    target.MemoryIndex,
                    "データセグメントが参照するメモリが存在しません。",
                    new(WasmProcessingStage.Validate, data.ByteOffset, null, 11)
                );
            }
            ConstantInitializer.Validate(
                target.OffsetExpression,
                WasmValueKind.I32,
                importedGlobals,
                references,
                11
            );
        }
    }

    /// <summary>
    /// 各定義関数を1回の型検査と線形化で処理し、全成功時だけコードを返す
    /// </summary>
    /// <param name="module">importと定義関数の型参照、global初期化式、リソース宣言、データセグメントの検証を終えたmodule</param>
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
                FunctionValidator.Validate(
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
    /// <param name="references">当該module検証で共有する参照情報</param>
    private static void ValidateInitializers(
        WasmModule module,
        ReferenceValidationContext references
    )
    {
        var importedGlobals = module.Imports.OfType<GlobalImport>().Select(x => x.Type).ToArray();
        foreach (var global in module.Globals)
        {
            ConstantInitializer.Validate(
                global.Initializer,
                global.Type.ValueKind,
                importedGlobals,
                references,
                6
            );
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
            throw ValidationDiagnostics.UnknownIndex(
                "function",
                start.FunctionIndex,
                "startが参照する関数が存在しません。",
                location
            );
        }

        var typeIndex =
            start.FunctionIndex < (uint)imports.Length
                ? imports[(int)start.FunctionIndex].TypeIndex
                : module.Functions[(int)(start.FunctionIndex - (uint)imports.Length)].TypeIndex;
        var type = module.Types[(int)typeIndex];
        if (!type.Parameters.IsEmpty || !type.Results.IsEmpty)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "start function must not have parameters or results",
                "startの関数型は引数・結果とも0個である必要があります。",
                location
            );
        }
    }

    /// <summary>
    /// 元の添字空間を保ってimportの型参照とリソース制約を逆順に検証する
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
            switch (import)
            {
                case FunctionImport:
                    functionCount++;
                    break;
                case TableImport:
                    tableCount++;
                    break;
                case MemoryImport:
                    memoryCount++;
                    break;
                case GlobalImport:
                    globalCount++;
                    break;
            }
        }

        var functionIndex = functionCount;
        for (var index = module.Imports.Length - 1; index >= 0; index--)
        {
            var import = module.Imports[index];
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
                            FunctionIndex = --functionIndex,
                        }
                    );
                    break;

                case TableImport table:
                    ValidateLimits(
                        table.Type.Limits,
                        uint.MaxValue,
                        "table size must be at most 2^32-1",
                        location
                    );
                    break;

                case MemoryImport memory:
                    ValidateMemory(memory.Type.Limits, location);
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
    /// 定義tableと定義memoryのlimitsを宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateResources(WasmModule module)
    {
        foreach (var table in module.Tables)
        {
            ValidateLimits(
                table.Limits,
                uint.MaxValue,
                "table size must be at most 2^32-1",
                new(WasmProcessingStage.Validate, table.ByteOffset, null, 4)
            );
        }
        foreach (var memory in module.Memories)
        {
            ValidateMemory(
                memory.Limits,
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
                var category = export.Kind switch
                {
                    WasmExternalKind.Function => "function",
                    WasmExternalKind.Table => "table",
                    WasmExternalKind.Memory => "memory",
                    WasmExternalKind.Global => "global",
                    _ => throw new InvalidOperationException(
                        "デコード済みexportの種類が不正です。"
                    ),
                };
                throw ValidationDiagnostics.UnknownIndex(
                    category,
                    export.Index,
                    "exportが参照する外部要素が存在しません。",
                    location
                );
            }
            if (!names.Add(export.Name))
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "duplicate export name",
                    "export名が重複しています。",
                    location
                );
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
            throw ValidationDiagnostics.UnknownIndex(
                "type",
                typeIndex,
                "参照する関数型が存在しません。",
                location
            );
        }
    }

    /// <summary>
    /// memoryのページ数のlimitsを検証する
    /// </summary>
    /// <param name="limits">未検証のページ数の範囲</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateMemory(WasmLimits limits, WasmFailureLocation location)
    {
        ValidateLimits(limits, 65536, "memory size must be at most 65536 pages (4GiB)", location);
    }

    /// <summary>
    /// memoryの総数を確認し、超過時は元の2個目の宣言位置を保持する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="importCount">importしたmemoryの個数</param>
    private static void ValidateMemoryCount(WasmModule module, uint importCount)
    {
        if (importCount + (uint)module.Memories.Length > 1)
        {
            var secondImport = module.Imports.OfType<MemoryImport>().Skip(1).FirstOrDefault();
            var offset =
                secondImport?.ByteOffset ?? module.Memories[importCount == 0 ? 1 : 0].ByteOffset;
            throw ValidationDiagnostics.ValidationFailure(
                "multiple memories",
                "memoryのimportと定義の合計が1個を超えています。",
                new(
                    WasmProcessingStage.Validate,
                    offset,
                    null,
                    secondImport is null ? (byte)5 : (byte)2
                )
            );
        }
    }

    /// <summary>
    /// リソースの割り当てや実装の保持上限とは区別して、limitsの仕様上の制約を検証する
    /// </summary>
    /// <param name="limits">未検証の範囲</param>
    /// <param name="maximum">仕様上の最大値</param>
    /// <param name="maximumPrefix">仕様上限を超えた場合の診断先頭</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateLimits(
        WasmLimits limits,
        uint maximum,
        string maximumPrefix,
        WasmFailureLocation location
    )
    {
        if (limits.Minimum > maximum)
        {
            throw ValidationDiagnostics.ValidationFailure(
                maximumPrefix,
                "limitsの最小値が仕様上限を超えています。",
                location
            );
        }
        if (limits.Maximum is { } max)
        {
            if (max > maximum)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    maximumPrefix,
                    "limitsの最大値が仕様上限を超えています。",
                    location
                );
            }
            if (limits.Minimum > max)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "size minimum must not be greater than maximum",
                    "limitsの最小値が最大値を超えています。",
                    location
                );
            }
        }
    }
}
