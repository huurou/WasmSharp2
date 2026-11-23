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
        foreach (var type in module.Types)
        {
            if (type.Unresolved is { } missing)
            {
                throw missing.ValidationFailure();
            }
        }
        var (functionCount, importedGlobalCount) = ValidateImportReferences(module);
        ValidateFunctionReferences(module, functionCount);
        var references = ReferenceValidationContext.Create(module, functionCount);
        WasmGlobalType[] globalTypes =
        [
            .. module.Imports.OfType<GlobalImport>().Select(x => x.Type),
            .. module.Globals.Select(x => x.Type),
        ];
        var segmentGlobalTypes = ValidateGlobalInitializers(
            module,
            references.FunctionTypes,
            globalTypes,
            (int)importedGlobalCount
        );
        ValidateResources(module, references.FunctionTypes, globalTypes.AsSpan(0, (int)importedGlobalCount));
        ValidateElements(module, references, segmentGlobalTypes);
        ValidateData(module, references, segmentGlobalTypes);
        var codes = ValidateFunctions(module, references, globalTypes);
        ValidateStart(module);
        ValidateExports(module, references, (uint)globalTypes.Length);
        ValidateMemoryCount(module, references.MemoryCount);
        return codes;
    }

    /// <summary>
    /// 全方式の要素式を検証し、アクティブの場合は転送先とオフセット式も検証する
    /// </summary>
    /// <param name="module">global初期化式とリソース宣言の検証を終えたmodule</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <param name="visibleGlobals">セグメントの定数式から参照できるglobalの型</param>
    private static void ValidateElements(
        WasmModule module,
        ReferenceValidationContext references,
        ReadOnlySpan<WasmGlobalType> visibleGlobals
    )
    {
        foreach (var element in module.ElementSegments)
        {
            var location = new WasmFailureLocation(WasmProcessingStage.Validate, element.ByteOffset, null, 9);
            element.ElementValueType.Validate();
            foreach (var initializer in element.Initializers)
            {
                ConstantInitializer.Validate(
                    initializer,
                    element.ElementValueType,
                    visibleGlobals,
                    references.FunctionTypes,
                    9,
                    module.SpecVersion
                );
            }
            var target = element.ActiveTarget;
            if (target is null)
            {
                continue;
            }
            if (target.TableIndex >= (uint)references.TableTypes.Length)
            {
                throw ValidationDiagnostics.UnknownIndex(
                    "table",
                    target.TableIndex,
                    "要素セグメントが参照するテーブルが存在しません。",
                    location
                );
            }
            var tableType = references.TableTypes[(int)target.TableIndex];
            if (!tableType.ElementValueType.Accepts(element.ElementValueType))
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "type mismatch",
                    "要素セグメントの参照型と対象テーブルの参照型が一致しません。",
                    location
                );
            }
            ConstantInitializer.Validate(
                target.OffsetExpression,
                tableType.AddressType,
                visibleGlobals,
                references.FunctionTypes,
                9,
                module.SpecVersion
            );
        }
    }

    /// <summary>
    /// アクティブデータセグメントのメモリ参照と、そのアドレス型に従うオフセット式を宣言順に検証する
    /// </summary>
    /// <param name="module">リソース宣言の検証を終えたmodule</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <param name="visibleGlobals">セグメントの定数式から参照できるglobalの型</param>
    private static void ValidateData(
        WasmModule module,
        ReferenceValidationContext references,
        ReadOnlySpan<WasmGlobalType> visibleGlobals
    )
    {
        foreach (var data in module.DataSegments)
        {
            var target = data.ActiveTarget;
            if (target is null)
            {
                continue;
            }
            if (target.MemoryIndex >= references.MemoryCount)
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
                references.MemoryAddressTypes[(int)target.MemoryIndex],
                visibleGlobals,
                references.FunctionTypes,
                11,
                module.SpecVersion
            );
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
        var importedFunctionCount = references.FunctionCount - (uint)module.Functions.Length;
        var codes = ImmutableArray.CreateBuilder<FunctionCode>(module.Functions.Length);
        for (var definitionIndex = 0; definitionIndex < module.Functions.Length; definitionIndex++)
        {
            codes.Add(
                FunctionValidator.Validate(
                    module,
                    definitionIndex,
                    importedFunctionCount + (uint)definitionIndex,
                    references.FunctionTypes,
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
    /// <param name="functionTypes">importを先頭にした関数添字順の型</param>
    /// <param name="globalTypes">importを先頭にしたglobal添字順の型</param>
    /// <param name="importedGlobalCount">importしたglobalの個数</param>
    /// <returns>すべてのglobalの検証後にセグメントの定数式から参照できる型</returns>
    private static ReadOnlySpan<WasmGlobalType> ValidateGlobalInitializers(
        WasmModule module,
        ReadOnlySpan<WasmFunctionType> functionTypes,
        ReadOnlySpan<WasmGlobalType> globalTypes,
        int importedGlobalCount
    )
    {
        var visibleGlobalCount = importedGlobalCount;
        foreach (var global in module.Globals)
        {
            global.Type.ValueType.Validate();
            ConstantInitializer.Validate(
                global.Initializer,
                global.Type.ValueType,
                globalTypes[..visibleGlobalCount],
                functionTypes,
                6,
                module.SpecVersion
            );
            if (module.SpecVersion == WasmSpecVersion.Core3)
            {
                visibleGlobalCount++;
            }
        }
        return globalTypes[..visibleGlobalCount];
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
    /// <returns>importした関数・globalの個数</returns>
    private static (uint FunctionCount, uint GlobalCount) ValidateImportReferences(WasmModule module)
    {
        uint functionCount = 0;
        uint globalCount = 0;
        foreach (var import in module.Imports)
        {
            switch (import)
            {
                case FunctionImport:
                    functionCount++;
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

                case GlobalImport global:
                    global.Type.ValueType.Validate();
                    break;

                case TableImport table:
                    ValidateTable(table.Definition.Type, location);
                    break;

                case MemoryImport memory:
                    ValidateMemory(memory.Definition.Type, location);
                    break;
            }
        }

        return (functionCount, globalCount);
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
    /// 定義tableの型と初期化式、定義memoryのlimitsを宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="functionTypes">テーブル初期化式から参照できる関数の型</param>
    /// <param name="importedGlobals">テーブル初期化式から参照できるimportしたglobalの型</param>
    private static void ValidateResources(
        WasmModule module,
        ReadOnlySpan<WasmFunctionType> functionTypes,
        ReadOnlySpan<WasmGlobalType> importedGlobals
    )
    {
        foreach (var table in module.Tables)
        {
            var location = new WasmFailureLocation(WasmProcessingStage.Validate, table.ByteOffset, null, 4);
            ValidateTable(table.Type, location);
            if (!table.Initializer.IsEmpty)
            {
                ConstantInitializer.Validate(
                    table.Initializer,
                    table.Type.ElementValueType,
                    importedGlobals,
                    functionTypes,
                    4,
                    module.SpecVersion
                );
            }
            else if (!table.Type.ElementValueType.IsNullable)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "type mismatch",
                    "テーブルの暗黙の初期値nullを要素型に格納できません。",
                    location
                );
            }
        }
        foreach (var memory in module.Memories)
        {
            ValidateMemory(memory.Type, new(WasmProcessingStage.Validate, memory.ByteOffset, null, 5));
        }
    }

    /// <summary>
    /// exportの参照先と名前の一意性を宣言順に検証する
    /// </summary>
    /// <param name="module">デコード済みの静的定義</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <param name="globalCount">importと定義を合わせたglobalの個数</param>
    private static void ValidateExports(WasmModule module, ReferenceValidationContext references, uint globalCount)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var export in module.Exports)
        {
            var (count, category) = export.Kind switch
            {
                WasmExternalKind.Function => (references.FunctionCount, "function"),
                WasmExternalKind.Table => ((uint)references.TableTypes.Length, "table"),
                WasmExternalKind.Memory => (references.MemoryCount, "memory"),
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
    /// アドレス型が許す最大要素数と、テーブルの上下限の大小関係を検証する
    /// </summary>
    private static void ValidateTable(WasmTableType type, WasmFailureLocation location)
    {
        type.ElementValueType.Validate();
        if (type.ExceedsElementLimit)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "table size must be at most 2^32-1 for i32",
                "32ビットテーブルの要素数が仕様上限を超えています。",
                location
            );
        }
        ValidateLimits(type.MinimumElements, type.MaximumElements, location);
    }

    /// <summary>
    /// memoryのページ数の仕様上限とlimitsの大小関係を検証する
    /// </summary>
    /// <param name="type">アドレス型と未検証のページ数の上下限</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateMemory(WasmMemoryType type, WasmFailureLocation location)
    {
        var minimumExceedsLimit = type.MinimumPages > type.PageLimit;
        if (minimumExceedsLimit || type.MaximumPages > type.PageLimit)
        {
            var diagnostic =
                type.AddressType == WasmValueKind.I32
                    ? "memory size must be at most 65536 pages (4GiB)"
                    : "memory size must be at most 2^48 pages for i64";
            var detail = minimumExceedsLimit ? "limitsの最小値" : "limitsの最大値";
            throw ValidationDiagnostics.ValidationFailure(diagnostic, detail + "が仕様上限を超えています。", location);
        }
        ValidateLimits(type.MinimumPages, type.MaximumPages, location);
    }

    /// <summary>
    /// Core 2.0では2個目のメモリの宣言位置で個数制限を報告する
    /// </summary>
    /// <param name="module">デコード時の仕様の版を保持する静的定義</param>
    /// <param name="memoryCount">importと定義を合わせたメモリの個数</param>
    private static void ValidateMemoryCount(WasmModule module, uint memoryCount)
    {
        if (module.SpecVersion != WasmSpecVersion.Core2 || memoryCount <= 1)
        {
            return;
        }

        var location = module
            .Imports.OfType<MemoryImport>()
            .Select(x => new WasmFailureLocation(WasmProcessingStage.Validate, x.ByteOffset, null, 2))
            .Concat(
                module.Memories.Select(x => new WasmFailureLocation(WasmProcessingStage.Validate, x.ByteOffset, null, 5))
            )
            .ElementAt(1);
        throw ValidationDiagnostics.ValidationFailure(
            "multiple memories",
            "memoryのimportと定義の合計が1個を超えています。",
            location
        );
    }

    /// <summary>
    /// limitsの最小値と最大値の大小関係を検証する
    /// </summary>
    /// <param name="minimum">最小値</param>
    /// <param name="maximum">最大値 指定がなければnull</param>
    /// <param name="location">宣言の位置</param>
    private static void ValidateLimits(ulong minimum, ulong? maximum, WasmFailureLocation location)
    {
        if (minimum > maximum)
        {
            throw ValidationDiagnostics.ValidationFailure(
                "size minimum must not be greater than maximum",
                "limitsの最小値が最大値を超えています。",
                location
            );
        }
    }
}
