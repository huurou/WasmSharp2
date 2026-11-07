using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal partial class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments("null")]
    [Arguments("\"\"")]
    [Arguments("\" \\t \"")]
    public async Task 命令名が空白またはnull_不完全宣言の診断を報告する(string name)
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 1, {{name}})]
                internal static partial class InstructionSet;
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
    }

    [Test]
    public async Task 不完全な宣言と同じopcodeの正常な宣言がある_不完全な宣言だけを診断する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "invalid", ImmediateKind.Unsupported, StackEffectKind.PushI32,
                    ValidationRule.Constant, "Handler")]
                [Instruction(0, 0x41, "valid")]
                internal static partial class InstructionSet;
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics[0].GetMessage()).Contains("'invalid'");
            await Assert
                .That(
                    diagnostics[0]
                        .Location.SourceTree!.GetText()
                        .ToString(diagnostics[0].Location.SourceSpan)
                )
                .Contains("\"invalid\"");
        }
    }

    [Test]
    public async Task 属性の数値引数が未解決_生成器例外を起こさず不完全宣言の診断を報告する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(InstructionSet.INVALID_PREFIX, 1, "invalid")]
                internal static partial class InstructionSet
                {
                    internal const byte INVALID_PREFIX = UnknownPrefix;
                }
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
        using (Assert.Multiple())
        {
            await Assert.That(driver.GetRunResult().Results.Single().Exception).IsNull();
            await Assert.That(diagnostics[0].GetMessage()).Contains("命令 ''");
        }
    }

    [Test]
    public async Task 命令名にXML特殊文字を含む_生成するドキュメントコメントに名前を保持する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "custom<&>", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.Handler))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)
                        => default;
                }
            }
            """,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out var diagnostics
        );

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        var opcode = output
            .GetTypeByMetadataName("WasmSharp.Instructions.ExecutionOpcode")!
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Single(x => x.HasConstantValue);
        var documentation = XDocument.Parse(opcode.GetDocumentationCommentXml()!);
        await Assert
            .That(documentation.Root!.Element("summary")!.Value.Trim())
            .IsEqualTo("custom<&>の実行opcode");
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var descriptor)
                && descriptor.Name == "custom<&>" && descriptor.ExecutionOpcode.HasValue;
            """
        );
    }
}
