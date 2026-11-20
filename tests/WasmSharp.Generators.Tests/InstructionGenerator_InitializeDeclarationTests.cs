using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
                .That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan))
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
    public async Task 数値入力配列が未解決の列挙子を参照する_生成器例外を起こさず不完全宣言の診断を報告する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 1, "invalid.numeric", ImmediateKind.None, StackEffectKind.Numeric,
                    ValidationRule.Numeric, "Handler", new WasmValueKind[] { WasmValueKind.I32 },
                    (WasmValueKind)0)]
                internal static partial class InstructionSet;
            }
            """
        );
        var valueKind = compilation.GetTypeByMetadataName("WasmSharp.WasmValueKind")!;
        var member = (EnumMemberDeclarationSyntax)
            valueKind.GetMembers("I32").Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var tree = member.SyntaxTree;
        var updatedRoot = tree.GetRoot()
            .ReplaceNode(
                member,
                member.WithEqualsValue(SyntaxFactory.EqualsValueClause(SyntaxFactory.IdentifierName("UnknownValue")))
            );
        compilation = compilation.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(updatedRoot, tree.Options));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
        using (Assert.Multiple())
        {
            await Assert.That(driver.GetRunResult().Results.Single().Exception).IsNull();
            await Assert.That(diagnostics[0].GetMessage()).Contains("'invalid.numeric'");
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task 定数式の二項演算で入力数が2個と異なる_命令の属性位置に不完全宣言の診断を報告する(int inputCount)
    {
        // Arrange
        var attribute = $"""
            Instruction(0, 0x6B, "i32.sub", ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Numeric,
                nameof(Execution.IntegerArithmetic.I32Sub), {inputCount}, WasmValueKind.I32, WasmValueKind.I32,
                IsConstantBinaryOperation = true)
            """;
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [{{attribute}}]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static class IntegerArithmetic
                {
                    internal static WasmValue I32Sub(WasmValue left, WasmValue right) => default;
                }
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
        await Assert
            .That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan))
            .IsEqualTo(attribute);
    }

    [Test]
    public async Task 属性の引数個数が一致しない_属性位置に不完全宣言の診断を報告して生成を中断する()
    {
        // Arrange
        const string ATTRIBUTE = "Instruction(0, 1)";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [{{ATTRIBUTE}}]
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
            await Assert.That(driver.GetRunResult().Results.Single().Exception).IsNull();
            await Assert
                .That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan))
                .IsEqualTo(ATTRIBUTE);
        }
    }

    [Test]
    public async Task Interpreter型が存在しない_属性位置にhandler不在の診断を報告して生成を中断する()
    {
        // Arrange
        const string ATTRIBUTE = """
            Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32, ValidationRule.Constant, "Handler")
            """;
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [{{ATTRIBUTE}}]
                internal static partial class InstructionSet;
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG003");
        using (Assert.Multiple())
        {
            await Assert.That(driver.GetRunResult().Results.Single().Exception).IsNull();
            await Assert
                .That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan))
                .IsEqualTo(ATTRIBUTE);
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        var opcode = output
            .GetTypeByMetadataName("WasmSharp.Instructions.ExecutionOpcode")!
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Single(x => x.HasConstantValue);
        var documentation = XDocument.Parse(opcode.GetDocumentationCommentXml()!);
        await Assert.That(documentation.Root!.Element("summary")!.Value.Trim()).IsEqualTo("custom<&>の実行opcode");
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var descriptor)
                && descriptor.Name == "custom<&>" && descriptor.ExecutionOpcode.HasValue;
            """
        );
    }
}
