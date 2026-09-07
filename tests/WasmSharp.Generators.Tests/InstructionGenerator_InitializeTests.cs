using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments(0x42, "i64.const", "I64", "PushI64")]
    [Arguments(0x43, "f32.const", "F32Bits", "PushF32")]
    [Arguments(0x44, "f64.const", "F64Bits", "PushF64")]
    public async Task 対応済み宣言のopcodeとhandlerを変更する_lookupと実行分岐が一緒に更新される(
        int code,
        string name,
        string immediate,
        string stackEffect
    )
    {
        // Arrange
        const string DECLARATION = """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.First))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(WasmExecutionContext context) => RunLoop(context, 0);
                    internal static ExecutionResult First(WasmExecutionContext context, in Instruction instruction)
                    {
                        context.Value = instruction.Immediate;
                        context.FrameCount--;
                        return default;
                    }
                    internal static ExecutionResult Second(WasmExecutionContext context, in Instruction instruction)
                    {
                        context.Value = new(instruction.Immediate.Bits + 1);
                        context.FrameCount--;
                        return default;
                    }
                }
            }
            """;
        var compilation = GeneratorTestSource.CreateCompilation(
            DECLARATION,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());
        var declarationTree = compilation.SyntaxTrees.Single(x => x.ToString() == DECLARATION);
        var updated = compilation.ReplaceSyntaxTree(
            declarationTree,
            CSharpSyntaxTree.ParseText(
                DECLARATION
                    .Replace("0x41", code.ToString())
                    .Replace("i32.const", name)
                    .Replace("ImmediateKind.I32", $"ImmediateKind.{immediate}")
                    .Replace("StackEffectKind.PushI32", $"StackEffectKind.{stackEffect}")
                    .Replace(
                        "nameof(Execution.Interpreter.First)",
                        "nameof(Execution.Interpreter.Second)"
                    )
            )
        );

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var originalOutput,
            out var originalDiagnostics
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(
            updated,
            out var output,
            out var diagnostics
        );

        // Assert
        await Assert.That(originalDiagnostics).IsEmpty();
        await Assert.That(diagnostics).IsEmpty();
        await GeneratorTestSource.AssertCompilesAndRuns(
            originalOutput,
            """
            WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var descriptor);
            var context = new WasmSharp.Execution.WasmExecutionContext
            {
                FrameCount = 1,
                Instructions = [new(descriptor.ExecutionOpcode!.Value, new(41), 100)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.Value.Bits == 41;
            """
        );
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, {{code}}), out var descriptor);
            var context = new WasmSharp.Execution.WasmExecutionContext
            {
                FrameCount = 1,
                Instructions = [new(descriptor.ExecutionOpcode!.Value, new(41), 100)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{stackEffect}}
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out _)
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.Value.Bits == 42;
            """
        );
    }

    [Test]
    public async Task handlerだけを不正なシグネチャへ変更する_同じdriverで診断が更新される()
    {
        // Arrange
        const string DECLARATION = """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.Handler))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(WasmExecutionContext context, in Instruction instruction)
                        => throw new System.NotSupportedException();
                }
            }
            """;
        var compilation = GeneratorTestSource.CreateCompilation(
            DECLARATION,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());
        driver = driver.RunGenerators(compilation);
        var declarationTree = compilation.SyntaxTrees.Single(x => x.ToString() == DECLARATION);
        var updated = compilation.ReplaceSyntaxTree(
            declarationTree,
            CSharpSyntaxTree.ParseText(
                DECLARATION.Replace("ExecutionResult Handler(", "void Handler(")
            )
        );

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(updated, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG004");
    }

    [Test]
    [Arguments(
        "ImmediateKind.Unsupported, StackEffectKind.PushI32, ValidationRule.Constant, \"Handler\""
    )]
    [Arguments(
        "ImmediateKind.I32, StackEffectKind.Unsupported, ValidationRule.Constant, \"Handler\""
    )]
    [Arguments(
        "ImmediateKind.I32, StackEffectKind.PushI32, ValidationRule.Unsupported, \"Handler\""
    )]
    [Arguments("ImmediateKind.I32, StackEffectKind.PushI32, ValidationRule.Constant, null")]
    [Arguments("ImmediateKind.I32, StackEffectKind.PushI32, ValidationRule.Constant, \"\"")]
    [Arguments("(ImmediateKind)999, StackEffectKind.PushI32, ValidationRule.Constant, \"Handler\"")]
    public async Task 対応済み行が不完全_属性位置付きのビルドエラーを報告する(string metadata)
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", {{metadata}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(WasmExecutionContext context, in Instruction instruction)
                        => default;
                }
            }
            """,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
    }

    [Test]
    [Arguments("", "WSIG003")]
    [Arguments(
        "static void Handler(WasmExecutionContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "ExecutionResult Handler(WasmExecutionContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(WasmExecutionContext context, Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(in WasmExecutionContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(in Instruction instruction, WasmExecutionContext context)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(WasmExecutionContext context, ref Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler<T>(WasmExecutionContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ref ExecutionResult Handler(WasmExecutionContext context, in Instruction instruction)",
        "WSIG004"
    )]
    public async Task handlerが存在しないか契約と異なる_属性位置付きのビルドエラーを報告する(
        string signature,
        string expectedId
    )
    {
        // Arrange
        var method =
            signature == ""
                ? ""
                : $"internal {signature} => throw new System.NotSupportedException();";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, "Handler")]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal partial class Interpreter
                {
                    {{method}}
                }
            }
            """,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, expectedId);
    }

    [Test]
    [Arguments("Trap", "Exceptions.WasmTrapReason.IntegerDivideByZero", "null", "null")]
    [Arguments("Exhaustion", "null", "Exceptions.WasmExhaustionReason.CallDepthLimit", "12")]
    public async Task handlerが失敗する_原因と上限と元位置をそのまま返して次の命令を実行しない(
        string status,
        string trapReason,
        string exhaustionReason,
        string limit
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "test.failure", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.Fail))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(WasmExecutionContext context) => RunLoop(context, 1);
                    internal static ExecutionResult Fail(WasmExecutionContext context, in Instruction instruction)
                        => new(ExecutionStatus.{{status}}, [], {{trapReason}}, {{exhaustionReason}},
                            {{limit}}, context.FunctionIndex, instruction.ByteOffset);
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
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var descriptor);
            var context = new WasmSharp.Execution.WasmExecutionContext
            {
                FrameCount = 2,
                Instructions = [new(descriptor.ExecutionOpcode!.Value, default, 12345678901L)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            var expected = WasmSharp.Execution.Interpreter.Fail(context, in context.Instructions[0]);
            return result == expected && result.Status == WasmSharp.Execution.ExecutionStatus.{{status}}
                && result.FunctionIndex == 7 && result.ByteOffset == 12345678901L
                && context.Pc == 1 && context.FrameCount == 2;
            """
        );
    }

    [Test]
    public async Task opcodeが重複する_属性位置付きのビルドエラーを報告する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "first")]
                [Instruction(0xFD, 1, "second")]
                internal static partial class InstructionSet;
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo("WSIG001");
            await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
            await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
            await Assert
                .That(
                    diagnostics[0]
                        .Location.SourceTree!.GetText()
                        .ToString(diagnostics[0].Location.SourceSpan)
                )
                .Contains("second");
            await Assert.That(driver.GetRunResult().GeneratedTrees).IsEmpty();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task 入口フレームが終了する_単一の生成ループが外側を実行せず停止する(
        int entryFrameCount
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.PushConstant))]
                [Instruction(0, 0x0B, "end", ImmediateKind.None, StackEffectKind.FunctionEnd,
                    ValidationRule.FunctionEnd, nameof(Execution.Interpreter.Return))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(WasmExecutionContext context, int entryFrameCount)
                        => RunLoop(context, entryFrameCount);
                    internal static ExecutionResult PushConstant(WasmExecutionContext context, in Instruction instruction)
                    {
                        context.Value = instruction.Immediate;
                        return default;
                    }
                    internal static ExecutionResult Return(WasmExecutionContext context, in Instruction instruction)
                    {
                        context.FrameCount--;
                        return default;
                    }
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
        await Assert.That(driver.GetRunResult().GeneratedTrees.Length).IsEqualTo(2);
        var loop = driver
            .GetRunResult()
            .GeneratedTrees.Single(x =>
                x.FilePath.EndsWith("Interpreter.g.cs", StringComparison.Ordinal)
            );
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    loop.GetRoot()
                        .DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.WhileStatementSyntax>()
                        .Count()
                )
                .IsEqualTo(1);
            await Assert
                .That(
                    loop.GetRoot()
                        .DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax>()
                        .Count()
                )
                .IsEqualTo(1);
        }
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var constant);
            WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x0B), out var end);
            var context = new WasmSharp.Execution.WasmExecutionContext
            {
                FrameCount = {{entryFrameCount + 1}},
                Instructions = [new(constant.ExecutionOpcode!.Value, new(42), 100),
                    new(end.ExecutionOpcode!.Value, default, 102)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context, {{entryFrameCount}});
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Value.Bits == 42 && context.Pc == 2 && context.FrameCount == {{entryFrameCount}};
            """
        );
    }

    [Test]
    public async Task 同じdriverで宣言を変更する_古い命令を除いて変更後のlookupを生成する()
    {
        // Arrange
        const string DECLARATION = """
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFC, 0, "before")]
                internal static partial class InstructionSet;
            }
            """;
        var compilation = GeneratorTestSource.CreateCompilation(DECLARATION);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());
        driver = driver.RunGenerators(compilation);
        var declarationTree = compilation.SyntaxTrees.Single(x => x.ToString() == DECLARATION);
        var updated = compilation.ReplaceSyntaxTree(
            declarationTree,
            CSharpSyntaxTree.ParseText(
                DECLARATION.Replace("0xFC, 0, \"before\"", "0xFD, 0xFFFFFFFF, \"after\\\"name\"")
            )
        );

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(
            updated,
            out var output,
            out var diagnostics
        );

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            return !WasmSharp.Instructions.InstructionSet.TryGet(new(0xFC, 0), out _)
                && WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, uint.MaxValue), out var descriptor)
                && descriptor.Opcode == new WasmSharp.Instructions.OpcodeKey(0xFD, uint.MaxValue)
                && descriptor.Name == "after\"name"
                && descriptor.ExecutionOpcode == null
                && System.Enum.GetValues<WasmSharp.Instructions.ExecutionOpcode>().Length == 0;
            """
        );
    }

    [Test]
    public async Task 命令宣言がない_ソースを生成しない()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation("");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That(driver.GetRunResult().GeneratedTrees).IsEmpty();
        }
    }

    [Test]
    public async Task 対応済みと未対応の宣言がある_命令情報と実行opcodeを生成する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "i32.const", ImmediateKind.I32, StackEffectKind.PushI32,
                    ValidationRule.Constant, nameof(Execution.Interpreter.PushConstant))]
                [Instruction(0xFC, 0, "i32.trunc_sat_f32_s")]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult PushConstant(WasmExecutionContext context, in Instruction instruction)
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
        await Assert.That(driver.GetRunResult().GeneratedTrees.Length).IsEqualTo(2);
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out var constant);
            var unsupportedFound = WasmSharp.Instructions.InstructionSet.TryGet(new(0xFC, 0), out var unsupported);
            return found && constant.Name == "i32.const"
                && constant.Immediate == WasmSharp.Instructions.ImmediateKind.I32
                && constant.StackEffect == WasmSharp.Instructions.StackEffectKind.PushI32
                && constant.Validation == WasmSharp.Instructions.ValidationRule.Constant
                && constant.ExecutionOpcode.HasValue
                && unsupportedFound && unsupported.Name == "i32.trunc_sat_f32_s"
                && unsupported.Immediate == WasmSharp.Instructions.ImmediateKind.Unsupported
                && unsupported.StackEffect == WasmSharp.Instructions.StackEffectKind.Unsupported
                && unsupported.Validation == WasmSharp.Instructions.ValidationRule.Unsupported
                && unsupported.ExecutionOpcode == null
                && System.Enum.GetValues<WasmSharp.Instructions.ExecutionOpcode>().Length == 1
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0), out _)
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, 0), out _);
            """
        );
    }
}
