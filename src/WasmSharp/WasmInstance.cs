using System.Collections.Immutable;

namespace WasmSharp;

/// <summary>
/// instanceを表現するクラス
/// </summary>
/// <remarks>Exportsは持たせない</remarks>
public sealed class WasmInstance
{
    /// <summary>
    /// このinstanceの元となる静的なmodule定義
    /// </summary>
    internal WasmModule Module { get; }

    /// <summary>
    /// このinstanceに所属する関数を関数index順に保持する不変配列
    /// </summary>
    internal ImmutableArray<WasmFunction> Functions { get; }

    /// <summary>
    /// このinstanceが新しい実行コンテキストを開くときのポリシー
    /// </summary>
    public WasmExecutionOptions ExecutionOptions { get; }

    /// <summary>
    /// module定義と実行ポリシーを保持し、所属する関数の実体を構築する
    /// </summary>
    /// <param name="module">元となる静的なmodule定義</param>
    /// <param name="options">新しい実行コンテキストを開くときの実行ポリシー</param>
    internal WasmInstance(WasmModule module, WasmExecutionOptions options)
    {
        Module = module;
        ExecutionOptions = options;
        var functions = ImmutableArray.CreateBuilder<WasmFunction>(module.Functions.Length);
        for (var i = 0; i < module.Functions.Length; i++)
        {
            functions.Add(new(this, (uint)i));
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
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のglobalを取得する
    /// </summary>
    /// <param name="name">globalのexport名</param>
    /// <returns>globalに格納されたvalue</returns>
    public WasmValue GetGlobal(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のmemoryを取得する
    /// </summary>
    /// <param name="name">memoryのexport名</param>
    /// <returns>memoru</returns>
    public WasmMemory GetMemory(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 指定したexport名のtableを取得する
    /// </summary>
    /// <param name="name">tableのexport名</param>
    /// <returns>table</returns>
    public WasmTable GetTable(string name)
    {
        throw new NotImplementedException();
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
