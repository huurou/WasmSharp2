using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// instanceを表現するクラス
/// </summary>
/// <remarks>Exportsは持たせない</remarks>
public sealed class WasmInstance
{
    private readonly ImmutableDictionary<string, ModuleExport> exports_;

    /// <summary>
    /// このinstanceの元となる静的なmodule定義
    /// </summary>
    internal WasmModule Module { get; }

    /// <summary>
    /// importを先頭に置き、定義関数を続けたmodule全体の関数index表
    /// </summary>
    internal ImmutableArray<WasmFunction> Functions { get; }

    /// <summary>
    /// importを先頭に置いたglobalの実体表
    /// </summary>
    internal ImmutableArray<WasmGlobal> Globals { get; }

    /// <summary>
    /// importを先頭に置いたmemoryの実体表
    /// </summary>
    internal ImmutableArray<WasmMemory> Memories { get; }

    /// <summary>
    /// importを先頭に置いたtableの実体表
    /// </summary>
    internal ImmutableArray<WasmTable> Tables { get; }

    /// <summary>
    /// このinstanceが新しい実行コンテキストを開くときのポリシー
    /// </summary>
    public WasmExecutionOptions ExecutionOptions { get; }

    /// <summary>
    /// importとリソース定義を持たないmoduleのinstanceを構築する
    /// </summary>
    /// <param name="module">元となる静的なmodule定義</param>
    /// <param name="options">新しい実行コンテキストを開くときの実行ポリシー</param>
    internal WasmInstance(WasmModule module, WasmExecutionOptions options)
        : this(module, options, [], [], [], []) { }

    /// <summary>
    /// importした関数を接続し、このinstanceに所属する定義関数を構築する
    /// </summary>
    internal WasmInstance(
        WasmModule module,
        WasmExecutionOptions options,
        ImmutableArray<WasmFunction> importedFunctions,
        ImmutableArray<WasmGlobal> globals,
        ImmutableArray<WasmMemory> memories,
        ImmutableArray<WasmTable> tables
    )
    {
        Module = module;
        ExecutionOptions = options;
        Globals = globals;
        Memories = memories;
        Tables = tables;
        exports_ = module.Exports.ToImmutableDictionary(x => x.Name, StringComparer.Ordinal);
        var functionCount = (long)importedFunctions.Length + module.Functions.Length;
        if (functionCount > Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "関数表が保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                new WasmFailureLocation(WasmProcessingStage.Instantiate, SectionId: 3)
            );
        }
        var functions = ImmutableArray.CreateBuilder<WasmFunction>((int)functionCount);
        functions.AddRange(importedFunctions);
        for (var i = 0; i < module.Functions.Length; i++)
        {
            functions.Add(new DefinedFunction(this, (uint)(importedFunctions.Length + i), (uint)i));
        }
        Functions = functions.MoveToImmutable();
    }

    /// <summary>
    /// 指定したexport名の関数を取得する
    /// </summary>
    /// <param name="name">関数のexport名</param>
    /// <returns>呼び出し対象の関数</returns>
    public WasmFunction GetFunction(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Module.FunctionExportIndices.TryGetValue(name, out var index))
        {
            throw new ArgumentException("指定した名前の関数exportが存在しません。", nameof(name));
        }
        return Functions[index];
    }

    /// <summary>
    /// 指定したexport名のglobalを取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>globalに格納されたvalue</returns>
    public WasmValue GetGlobal(string name)
    {
        return Globals[GetExportIndex(name, WasmExternalKind.Global)].Value;
    }

    /// <summary>
    /// 指定したexport名の共有global実体を取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>共有されるglobalの実体</returns>
    public WasmGlobal GetGlobalResource(string name)
    {
        return Globals[GetExportIndex(name, WasmExternalKind.Global)];
    }

    /// <summary>
    /// 指定したexport名のmemoryを取得する
    /// </summary>
    /// <param name="name">memoryのexport名</param>
    /// <returns>memory</returns>
    public WasmMemory GetMemory(string name)
    {
        return Memories[GetExportIndex(name, WasmExternalKind.Memory)];
    }

    /// <summary>
    /// 指定したexport名のtableを取得する
    /// </summary>
    /// <param name="name">tableのexport名</param>
    /// <returns>table</returns>
    public WasmTable GetTable(string name)
    {
        return Tables[GetExportIndex(name, WasmExternalKind.Table)];
    }

    private int GetExportIndex(string name, WasmExternalKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!exports_.TryGetValue(name, out var export) || export.Kind != kind)
        {
            throw new ArgumentException("指定した名前と種類のexportが存在しません。", nameof(name));
        }
        return (int)export.Index;
    }

    /// <summary>
    /// 指定したexport名のtagを取得する
    /// </summary>
    /// <param name="name">tagのexport名</param>
    /// <returns>tag</returns>
    public WasmTag GetTag(string name)
    {
        throw new NotImplementedException();
    }
}
