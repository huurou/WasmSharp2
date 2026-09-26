using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// moduleの定義から構築した関数・リソースと、importした共有実体を保持するinstance
/// </summary>
/// <remarks>exportは名前で取得する。同じ対象の別名export・繰り返し取得・再exportは、同じ関数またはリソース実体を返す</remarks>
public sealed class WasmInstance
{
    /// <summary>
    /// export名から種類とmodule内の添字への対応
    /// </summary>
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
    /// <param name="module">元となる検証済みのmodule定義</param>
    /// <param name="options">新しい実行コンテキストを開くときの実行ポリシー</param>
    /// <param name="importedFunctions">関数のimport宣言順に並ぶ共有実体</param>
    /// <param name="globals">importを先頭に置き、定義を続けた初期化済みglobalの実体表</param>
    /// <param name="memories">importを先頭に置き、定義を続けた割当済みmemoryの実体表</param>
    /// <param name="tables">importを先頭に置き、定義を続けた割当済みtableの実体表</param>
    /// <exception cref="WasmImplementationLimitException">importと定義を合わせた関数数が保持上限を超える場合</exception>
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
    /// <remarks>定義関数の所属instanceは変わらない。ホスト関数を取得元instanceへ固定せず、同じ実体を返す</remarks>
    /// <param name="name">関数のexport名</param>
    /// <returns>呼び出し対象の関数</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名の関数exportが存在しない場合</exception>
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
    /// 指定したexport名のglobalに格納された現在値を取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>globalに格納されたvalue</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名のglobal exportが存在しない場合</exception>
    public WasmValue GetGlobal(string name)
    {
        return Globals[GetExportIndex(name, WasmExternalKind.Global)].Value;
    }

    /// <summary>
    /// 指定したexport名の共有global実体を取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>共有されるglobalの実体</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名のglobal exportが存在しない場合</exception>
    public WasmGlobal GetGlobalResource(string name)
    {
        return Globals[GetExportIndex(name, WasmExternalKind.Global)];
    }

    /// <summary>
    /// 指定したexport名のmemoryを取得する
    /// </summary>
    /// <param name="name">memoryのexport名</param>
    /// <returns>内容とサイズの更新を共有するmemory実体</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名のmemory exportが存在しない場合</exception>
    public WasmMemory GetMemory(string name)
    {
        return Memories[GetExportIndex(name, WasmExternalKind.Memory)];
    }

    /// <summary>
    /// 指定したexport名のtableを取得する
    /// </summary>
    /// <param name="name">tableのexport名</param>
    /// <returns>要素と要素数の更新を共有するtable実体</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名のtable exportが存在しない場合</exception>
    public WasmTable GetTable(string name)
    {
        return Tables[GetExportIndex(name, WasmExternalKind.Table)];
    }

    /// <summary>
    /// export名と種類を照合し、対応する実体表の添字を取得する
    /// </summary>
    /// <param name="name">大文字・小文字を区別して完全一致で照合するexport名</param>
    /// <param name="kind">取得する外部要素の種類</param>
    /// <returns>指定種類の実体表における添字</returns>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">指定名が存在しないか、種類が一致しない場合</exception>
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
    /// tagの取得操作。現在は未実装
    /// </summary>
    /// <param name="name">tagのexport名</param>
    /// <returns>現在は値を返さず、常に例外を送出する</returns>
    /// <exception cref="NotImplementedException">常に送出する</exception>
    public WasmTag GetTag(string name)
    {
        throw new NotImplementedException();
    }
}
