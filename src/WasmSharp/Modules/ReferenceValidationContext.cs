using System.Collections.Immutable;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// 1回のmodule検証で共有する参照の添字型と宣言集合
/// </summary>
internal sealed class ReferenceValidationContext
{
    /// <summary>
    /// importと定義を合わせた関数の個数
    /// </summary>
    internal uint FunctionCount => (uint)FunctionTypes.Length;

    /// <summary>
    /// importを先頭にした関数添字順の型
    /// </summary>
    internal WasmFunctionType[] FunctionTypes { get; }

    /// <summary>
    /// importと定義を合わせたメモリの個数
    /// </summary>
    internal uint MemoryCount => (uint)MemoryAddressTypes.Length;

    /// <summary>
    /// importを先頭にしたメモリ添字順のアドレス型
    /// </summary>
    internal ImmutableArray<WasmValueKind> MemoryAddressTypes { get; }

    /// <summary>
    /// importを先頭にしたテーブル添字順の型
    /// </summary>
    internal ImmutableArray<WasmTableType> TableTypes { get; }

    /// <summary>
    /// element添字順の要素型
    /// </summary>
    internal ImmutableArray<WasmValueType> ElementTypes { get; }

    /// <summary>
    /// 関数本体とstartを除いた定義が参照する未検証の関数添字集合
    /// </summary>
    internal ImmutableHashSet<uint> DeclaredFunctions { get; }

    /// <summary>
    /// 呼び出し内で構築した不変情報を保持する
    /// </summary>
    private ReferenceValidationContext(
        WasmFunctionType[] functionTypes,
        ImmutableArray<WasmValueKind> memoryAddressTypes,
        ImmutableArray<WasmTableType> tableTypes,
        ImmutableArray<WasmValueType> elementTypes,
        ImmutableHashSet<uint> declaredFunctions
    )
    {
        FunctionTypes = functionTypes;
        MemoryAddressTypes = memoryAddressTypes;
        TableTypes = tableTypes;
        ElementTypes = elementTypes;
        DeclaredFunctions = declaredFunctions;
    }

    /// <summary>
    /// 添字の存在や式の型を検査せず、検証に使う型列と宣言集合を構築する
    /// </summary>
    /// <param name="module">importと関数型参照の検査を終えた静的定義</param>
    /// <param name="importedFunctionCount">import参照の検査時に数えた関数の個数</param>
    /// <returns>moduleや実行コードへ保存しない、当該検証だけで共有する情報</returns>
    internal static ReferenceValidationContext Create(WasmModule module, uint importedFunctionCount)
    {
        var functionTypes = new WasmFunctionType[importedFunctionCount + module.Functions.Length];
        var functionIndex = 0;
        foreach (var import in module.Imports.OfType<FunctionImport>())
        {
            functionTypes[functionIndex++] = module.FunctionTypeAt(import.TypeIndex);
        }
        foreach (var function in module.Functions)
        {
            functionTypes[functionIndex++] = module.FunctionTypeAt(function.TypeIndex);
        }

        var declaredFunctions = ImmutableHashSet.CreateBuilder<uint>();
        foreach (var table in module.Tables)
        {
            CollectFunctions(table.Initializer);
        }
        foreach (var global in module.Globals)
        {
            CollectFunctions(global.Initializer);
        }
        foreach (var element in module.ElementSegments)
        {
            foreach (var initializer in element.Initializers)
            {
                CollectFunctions(initializer);
            }
            if (element.ActiveTarget is { } target)
            {
                CollectFunctions(target.OffsetExpression);
            }
        }
        foreach (var data in module.DataSegments)
        {
            if (data.ActiveTarget is { } target)
            {
                CollectFunctions(target.OffsetExpression);
            }
        }
        foreach (var export in module.Exports)
        {
            if (export.Kind == WasmExternalKind.Function)
            {
                declaredFunctions.Add(export.Index);
            }
        }

        return new ReferenceValidationContext(
            functionTypes,
            [
                .. module.Imports.OfType<MemoryImport>().Select(x => x.Definition.Type.AddressType),
                .. module.Memories.Select(x => x.Type.AddressType),
            ],
            [.. module.Imports.OfType<TableImport>().Select(x => x.Definition.Type), .. module.Tables.Select(x => x.Type)],
            [.. module.ElementSegments.Select(x => x.ElementValueType)],
            declaredFunctions.ToImmutable()
        );

        void CollectFunctions(ImmutableArray<DecodedInstruction> expression)
        {
            foreach (var instruction in expression)
            {
                // 固定仕様のFree.instrと同じくcallも収集する 定数性と添字の検査は後続段階で行う。
                if (instruction.Opcode is { Prefix: 0, Code: 0x10 or 0xD2 })
                {
                    declaredFunctions.Add(instruction.Index);
                }
            }
        }
    }
}
