using Microsoft.CodeAnalysis;

namespace WasmSharp.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class InstructionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 命令宣言のない初期構成ではソースを生成しない。
    }
}
