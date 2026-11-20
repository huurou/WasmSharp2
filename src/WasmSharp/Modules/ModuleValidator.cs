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
        var (functionCount, memoryCount, importedGlobalCount) = ValidateImportReferences(module);
        ValidateFunctionReferences(module, functionCount);
        var references = ReferenceValidationContext.Create(module, functionCount);
        WasmGlobalType[] globalTypes =
        [
            .. module.Imports.OfType<GlobalImport>().Select(x => x.Type),
            .. module.Globals.Select(x => x.Type),
        ];
        ValidateInitializers(module, references.FunctionCount, globalTypes, (int)importedGlobalCount);
        ValidateResources(module);
        ValidateElements(module, references);
        ValidateData(module, memoryCount, references.FunctionCount);
        var codes = ValidateFunctions(module, references, globalTypes);
        ValidateStart(module);
        ValidateExports(
            module,
            references.FunctionCount,
            (uint)references.TableElementTypes.Length,
            memoryCount + (uint)module.Memories.Length,
            (uint)globalTypes.Length
        );
        ValidateMemoryCount(module, memoryCount);
        return codes;
    }

    /// <summary>
    /// 全方式の要素式を検証し、アクティブの場合は転送先とオフセット式も検証する
    /// </summary>
    /// <param name="module">global初期化式とリソース宣言の検証を終えたmodule</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    private static void ValidateElements(WasmModule module, ReferenceValidationContext references)
    {
        var importedGlobals = module.Imports.OfType<GlobalImport>().Select(x => x.Type).ToArray();
        foreach (var element in module.ElementSegments)
        {
            foreach (var initializer in element.Initializers)
            {
                ConstantInitializer.Validate(initializer, element.ElementType, importedGlobals, references.FunctionCount, 9);
            }
            var target = element.ActiveTarget;
            if (target is null)
            {
                continue;
            }
            var location = new WasmFailureLocation(WasmProcessingStage.Validate, element.ByteOffset, null, 9);
            if (target.TableIndex >= (uint)references.TableElementTypes.Length)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "table",
                    target.TableIndex,
                    "要素セグメントが参照するテーブルが存在しません。",
                    location
                );
            }
            if (element.ElementType != references.TableElementTypes[(int)target.TableIndex])
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "type mismatch",
                    "要素セグメントの参照型と対象テーブルの参照型が一致しません。",
                    location
                );
            }
            ConstantInitializer.Validate(
                target.OffsetExpression,
                WasmValueKind.I32,
                importedGlobals,
                references.FunctionCount,
                9
            );
        }
    }

    /// <summary>
    /// アクティブデータセグメントのメモリ参照とi32のオフセット式を宣言順に検証する
    /// </summary>
    /// <param name="module">リソース宣言の検証を終えたmodule</param>
    /// <param name="importedMemoryCount">importしたmemoryの個数</param>
    /// <param name="functionCount">importと定義を合わせた関数の個数</param>
    private static void ValidateData(WasmModule module, uint importedMemoryCount, uint functionCount)
    {
        var memoryCount = importedMemoryCount + (uint)module.Memories.Length;
        var importedGlobals = module.Imports.OfType<GlobalImport>().Select(x => x.Type).ToArray();
        foreach (var data in module.DataSegments)
        {
            var target = data.ActiveTarget;
            if (target is null)
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
            ConstantInitializer.Validate(target.OffsetExpression, WasmValueKind.I32, importedGlobals, functionCount, 11);
        }
    }

    /// <summary>
    /// 各定義関数を1回の型検査と線形化で処理し、全成功時だけコードを返す
    /// </summary>
    /// <param name="module">importと定義関数の型参照、global初期化式、リソース宣言、要素・データセグメントの検証を終えたmodule</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <param name="globalTypes">importを先頭にしたglobal添字順の型</param>
    /// <returns>ローカルに保持した定義順の線形実行コード</returns>
    private static ImmutableArray<FunctionCode> ValidateFunctions(
        WasmModule module,
        ReferenceValidationContext references,
        WasmGlobalType[] globalTypes
    )
    {
        var importedFunctionCount = (uint)module.Imports.Count(x => x is FunctionImport);
        WasmFunctionType[] functionTypes =
        [
            .. module.Imports.OfType<FunctionImport>().Select(x => module.Types[(int)x.TypeIndex]),
            .. module.Functions.Select(x => module.Types[(int)x.TypeIndex]),
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
                    globalTypes,
                    references
                )
            );
        }

        return codes.MoveToImmutable();
    }

    /// <summary>
    /// globalの初期化式を評価せず、1個の宣言型の値を生成することを検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="functionCount">importと定義を合わせた関数の個数</param>
    /// <param name="globalTypes">importを先頭にしたglobal添字順の型</param>
    /// <param name="importedGlobalCount">importしたglobalの個数</param>
    private static void ValidateInitializers(
        WasmModule module,
        uint functionCount,
        ReadOnlySpan<WasmGlobalType> globalTypes,
        int importedGlobalCount
    )
    {
        for (var index = 0; index < module.Globals.Length; index++)
        {
            var global = module.Globals[index];
            var visibleGlobalCount =
                module.SpecVersion == WasmSpecVersion.Core3 ? importedGlobalCount + index : importedGlobalCount;
            ConstantInitializer.Validate(
                global.Initializer,
                global.Type.ValueKind,
                globalTypes[..visibleGlobalCount],
                functionCount,
                6,
                module.SpecVersion
            );
        }
    }

    /// <summary>
    /// importと定義の関数添字空間でstartの型が引数・結果とも0個であることを検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    private static void ValidateStart(WasmModule module)
    {
        if (module.Start is null)
        {
            return;
        }

        var start = module.Start.Value;
        var imports = module.Imports.OfType<FunctionImport>().ToArray();
        var location = new WasmFailureLocation(WasmProcessingStage.Validate, start.ByteOffset, start.FunctionIndex, 8);
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
    /// <returns>importした関数・memory・globalの個数</returns>
    private static (uint FunctionCount, uint MemoryCount, uint GlobalCount) ValidateImportReferences(WasmModule module)
    {
        uint functionCount = 0;
        uint memoryCount = 0;
        uint globalCount = 0;
        foreach (var import in module.Imports)
        {
            switch (import)
            {
                case FunctionImport:
                    functionCount++;
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
            var location = new WasmFailureLocation(WasmProcessingStage.Validate, import.ByteOffset, null, 2);
            switch (import)
            {
                case FunctionImport function:
                    ValidateTypeIndex(module, function.TypeIndex, location with { FunctionIndex = --functionIndex });
                    break;

                case TableImport table:
                    ValidateLimits(table.Type.Limits, location);
                    break;

                case MemoryImport memory:
                    ValidateMemory(memory.Type.Limits, location);
                    break;
            }
        }

        return (functionCount, memoryCount, globalCount);
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
            ValidateLimits(table.Limits, new(WasmProcessingStage.Validate, table.ByteOffset, null, 4));
        }
        foreach (var memory in module.Memories)
        {
            ValidateMemory(memory.Limits, new(WasmProcessingStage.Validate, memory.ByteOffset, null, 5));
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
            var (count, category) = export.Kind switch
            {
                WasmExternalKind.Function => (functionCount, "function"),
                WasmExternalKind.Table => (tableCount, "table"),
                WasmExternalKind.Memory => (memoryCount, "memory"),
                WasmExternalKind.Global => (globalCount, "global"),
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
    private static void ValidateTypeIndex(WasmModule module, uint typeIndex, WasmFailureLocation location)
    {
        if (typeIndex >= (uint)module.Types.Length)
        {
            throw ValidationDiagnostics.UnknownIndex("type", typeIndex, "参照する関数型が存在しません。", location);
        }
    }

    /// <summary>
    /// memoryのページ数の仕様上限とlimitsの大小関係を検証する
    /// </summary>
    /// <param name="limits">未検証のページ数の範囲</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateMemory(WasmLimits limits, WasmFailureLocation location)
    {
        const uint MAXIMUM_PAGES = 65536;
        const string MAXIMUM_PREFIX = "memory size must be at most 65536 pages (4GiB)";

        if (limits.Minimum > MAXIMUM_PAGES)
        {
            throw ValidationDiagnostics.ValidationFailure(
                MAXIMUM_PREFIX,
                "limitsの最小値が仕様上限を超えています。",
                location
            );
        }
        if (limits.Maximum is > MAXIMUM_PAGES)
        {
            throw ValidationDiagnostics.ValidationFailure(
                MAXIMUM_PREFIX,
                "limitsの最大値が仕様上限を超えています。",
                location
            );
        }
        ValidateLimits(limits, location);
    }

    /// <summary>
    /// memoryの総数を確認し、2個以上の場合は元の2個目の宣言位置を保持する
    /// </summary>
    /// <remarks>Core 2.0では規則違反、複数メモリを許すCore 3.0では未実装として通知する</remarks>
    /// <param name="module">デコード時の仕様の版を保持する静的定義</param>
    /// <param name="importCount">importしたmemoryの個数</param>
    private static void ValidateMemoryCount(WasmModule module, uint importCount)
    {
        if (importCount + (uint)module.Memories.Length > 1)
        {
            var secondImport = module.Imports.OfType<MemoryImport>().Skip(1).FirstOrDefault();
            var offset = secondImport?.ByteOffset ?? module.Memories[importCount == 0 ? 1 : 0].ByteOffset;
            var location = new WasmFailureLocation(
                WasmProcessingStage.Validate,
                offset,
                null,
                secondImport is null ? (byte)5 : (byte)2
            );
            if (module.SpecVersion == WasmSpecVersion.Core3)
            {
                throw new WasmUnsupportedFeatureException(
                    "未実装の機能に遭遇しました。",
                    "multiple memories",
                    location,
                    [
                        new WasmUnverifiedRange(
                            WasmProcessingStage.Validate,
                            0,
                            module.InputLength,
                            "複数メモリを前提とする検証が未完了です。"
                        ),
                    ]
                );
            }

            throw ValidationDiagnostics.ValidationFailure(
                "multiple memories",
                "memoryのimportと定義の合計が1個を超えています。",
                location
            );
        }
    }

    /// <summary>
    /// limitsの最小値と最大値の大小関係を検証する
    /// </summary>
    /// <param name="limits">未検証の範囲</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateLimits(WasmLimits limits, WasmFailureLocation location)
    {
        if (limits.Maximum is { } maximum && limits.Minimum > maximum)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "size minimum must not be greater than maximum",
                "limitsの最小値が最大値を超えています。",
                location
            );
        }
    }
}
