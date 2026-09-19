using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules.ExternalValues;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// moduleをインスタンス化する
/// </summary>
internal static class ModuleInstantiator
{
    /// <summary>
    /// moduleをインスタンス化する
    /// </summary>
    internal static WasmInstance Instantiate(
        WasmModule module,
        WasmImports imports,
        WasmExecutionOptions options
    )
    {
        var linked = Link(module, imports);
        if (module.Start is { } start)
        {
            throw new WasmUnsupportedFeatureException(
                "startの実行は未実装です。",
                "section.start",
                new WasmFailureLocation(WasmProcessingStage.Instantiate, start.ByteOffset, null, 8),
                []
            );
        }

        var tables = CreateIndex(
            linked.OfType<TableExternalValue>().Select(x => x.Value),
            module.Tables.Length,
            4
        );
        foreach (var table in module.Tables)
        {
            try
            {
                tables.Add(new WasmTable(table.ElementKind, table.Limits));
            }
            catch (WasmImplementationLimitException exception)
            {
                throw new WasmImplementationLimitException(
                    exception.Message,
                    exception.Reason,
                    exception.Limit,
                    new WasmFailureLocation(
                        WasmProcessingStage.Instantiate,
                        table.ByteOffset,
                        null,
                        4
                    ),
                    exception
                );
            }
        }
        var memories = CreateIndex(
            linked.OfType<MemoryExternalValue>().Select(x => x.Value),
            module.Memories.Length,
            5
        );
        foreach (var memory in module.Memories)
        {
            memories.Add(new WasmMemory(memory.Limits));
        }
        var globals = CreateIndex(
            linked.OfType<GlobalExternalValue>().Select(x => x.Value),
            module.Globals.Length,
            6
        );
        foreach (var global in module.Globals)
        {
            var initializer = global.Initializer[0];
            var value =
                initializer.Opcode == new OpcodeKey(0, 0x23)
                    ? globals[(int)initializer.Index].Value
                    : initializer.Immediate;
            globals.Add(new WasmGlobal(global.Type, value));
        }
        return new WasmInstance(
            module,
            options,
            [.. linked.OfType<FunctionExternalValue>().Select(x => x.Value)],
            globals.MoveToImmutable(),
            memories.MoveToImmutable(),
            tables.MoveToImmutable()
        );
    }

    private static ImmutableArray<T>.Builder CreateIndex<T>(
        IEnumerable<T> imports,
        int definitionCount,
        byte sectionId
    )
    {
        var values = imports.ToImmutableArray();
        var count = (long)values.Length + definitionCount;
        if (count > Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "実体表が保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                new WasmFailureLocation(WasmProcessingStage.Instantiate, SectionId: sectionId)
            );
        }
        var index = ImmutableArray.CreateBuilder<T>((int)count);
        index.AddRange(values);
        return index;
    }

    /// <summary>
    /// importを解決する
    /// </summary>
    internal static ImmutableArray<WasmExternalValue> Link(WasmModule module, WasmImports imports)
    {
        var providers = imports.Snapshot();
        var linked = ImmutableArray.CreateBuilder<WasmExternalValue>(module.Imports.Length);
        for (var ordinal = 0; ordinal < module.Imports.Length; ordinal++)
        {
            var import = module.Imports[ordinal];
            if (
                !providers.TryGetValue(import.ModuleName, out var items)
                || !items.TryGetValue(import.Name, out var value)
            )
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.MissingImport,
                    "名前に一致する提供登録がありません。"
                );
            }
            var actualKind = value switch
            {
                FunctionExternalValue => WasmExternalKind.Function,
                GlobalExternalValue => WasmExternalKind.Global,
                MemoryExternalValue => WasmExternalKind.Memory,
                TableExternalValue => WasmExternalKind.Table,
                _ => throw new InvalidOperationException("外部要素の種類が不明です。"),
            };
            if (actualKind != import.Kind)
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.KindMismatch,
                    $"要求種類 {import.Kind} に対して提供種類は {actualKind} です。"
                );
            }
            var matches = (import, value) switch
            {
                (FunctionImport required, FunctionExternalValue provided) => module
                    .Types[(int)required.TypeIndex]
                    .Parameters.SequenceEqual(provided.Value.Type.Parameters)
                    && module
                        .Types[(int)required.TypeIndex]
                        .Results.SequenceEqual(provided.Value.Type.Results),
                (GlobalImport required, GlobalExternalValue provided) => required.Type
                    == provided.Value.Type,
                (MemoryImport required, MemoryExternalValue provided) => MatchesLimits(
                    required.Type.Limits,
                    provided.Value.PageCount,
                    provided.Value.MaximumPages
                ),
                (TableImport required, TableExternalValue provided) => required.Type.ElementKind
                    == provided.Value.ElementType
                    && MatchesLimits(
                        required.Type.Limits,
                        provided.Value.Count,
                        provided.Value.MaximumElements
                    ),
                _ => throw new InvalidOperationException("外部要素の種類が一致しません。"),
            };
            if (!matches)
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.TypeMismatch,
                    $"要求型 {DescribeImport(module, import)} に対して提供型は {DescribeValue(value)} です。"
                );
            }
            linked.Add(value);
        }
        return linked.MoveToImmutable();
    }

    private static string DescribeImport(WasmModule module, ModuleImport import)
    {
        return import switch
        {
            FunctionImport function => DescribeFunction(module.Types[(int)function.TypeIndex]),
            GlobalImport global => global.Type.ToString(),
            MemoryImport memory => memory.Type.Limits.ToString(),
            TableImport table => $"{table.Type.ElementKind} {table.Type.Limits}",
            _ => throw new InvalidOperationException("importの種類が不明です。"),
        };
    }

    private static string DescribeValue(WasmExternalValue value)
    {
        return value switch
        {
            FunctionExternalValue function => DescribeFunction(function.Value.Type),
            GlobalExternalValue global => global.Value.Type.ToString(),
            MemoryExternalValue memory => new WasmLimits(
                memory.Value.PageCount,
                memory.Value.MaximumPages
            ).ToString(),
            TableExternalValue table =>
                $"{table.Value.ElementType} {new WasmLimits(table.Value.Count, table.Value.MaximumElements)}",
            _ => throw new InvalidOperationException("外部要素の種類が不明です。"),
        };
    }

    private static string DescribeFunction(WasmFunctionType type)
    {
        return $"[{string.Join(", ", type.Parameters)}] -> [{string.Join(", ", type.Results)}]";
    }

    private static bool MatchesLimits(WasmLimits required, uint current, uint? maximum)
    {
        return current >= required.Minimum
            && (required.Maximum is null || maximum is { } actual && actual <= required.Maximum);
    }

    private static WasmInstantiateException LinkFailure(
        ModuleImport import,
        int ordinal,
        WasmInstantiateReason reason,
        string message
    )
    {
        return new WasmInstantiateException(
            message,
            reason,
            ordinal,
            import.ModuleName,
            import.Name,
            import.Kind,
            new WasmFailureLocation(WasmProcessingStage.Instantiate, import.ByteOffset, null, 2)
        );
    }
}
