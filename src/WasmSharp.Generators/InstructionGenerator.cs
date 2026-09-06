using Microsoft.CodeAnalysis;

namespace WasmSharp.Generators;

/// <summary>
/// Wasm命令の宣言からソースを生成するインクリメンタルジェネレーター
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class InstructionGenerator : IIncrementalGenerator
{
    /// <summary>
    /// ソース生成の処理を初期化する
    /// </summary>
    /// <param name="context">ソース生成の初期化コンテキスト</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 命令宣言のない初期構成ではソースを生成しない。
    }
}
