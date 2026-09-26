using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments(0x00, "unreachable", "None", "Unreachable")]
    [Arguments(0x10, "call", "Index", "Call")]
    [Arguments(0x0F, "return", "None", "Return")]
    [Arguments(0x1A, "drop", "None", "Drop")]
    [Arguments(0x20, "local.get", "Index", "LocalGet")]
    [Arguments(0x21, "local.set", "Index", "LocalSet")]
    [Arguments(0x22, "local.tee", "Index", "LocalTee")]
    [Arguments(0x23, "global.get", "Index", "GlobalGet")]
    [Arguments(0x24, "global.set", "Index", "GlobalSet")]
    public async Task ホスト連携の命令情報を宣言する_実ソースの添字と値即値を独立してhandlerへ渡す(
        int code,
        string name,
        string immediate,
        string rule
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, {{code}}, "{{name}}", ImmediateKind.{{immediate}},
                    StackEffectKind.{{rule}}, ValidationRule.{{rule}}, nameof(Execution.Interpreter.Handler))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = new(instruction.Immediate.Bits + instruction.Index);
                        context.CompleteFrame();
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
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, {{code}}), out var descriptor);
            var decoded = new WasmSharp.Modules.DecodedInstruction(new(0, {{code}}), new(42), 12345678901L, uint.MaxValue);
            var instruction = new WasmSharp.Execution.Instruction(descriptor.ExecutionOpcode!.Value,
                decoded.Immediate, decoded.ByteOffset, decoded.Index);
            var context = new WasmSharp.Execution.InterpreterContext(1) { Instructions = [instruction] };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && decoded.Opcode == new WasmSharp.Instructions.OpcodeKey(0, {{code}})
                && decoded.Index == uint.MaxValue && instruction.Index == uint.MaxValue
                && decoded.Immediate.Bits == 42 && instruction.Immediate.Bits == 42
                && instruction.ByteOffset == 12345678901L
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Value.Bits == 4294967337L;
            """
        );
    }

    [Test]
    public async Task ランタイムと同じホスト連携の宣言を実行する_各opcodeを宣言したhandlerへ添字付きで分岐しreturnとtrapで停止する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x00, "unreachable", ImmediateKind.None, StackEffectKind.Unreachable,
                    ValidationRule.Unreachable, nameof(Execution.Interpreter.Unreachable))]
                [Instruction(0, 0x0F, "return", ImmediateKind.None, StackEffectKind.Return,
                    ValidationRule.Return, nameof(Execution.Interpreter.Return))]
                [Instruction(0, 0x10, "call", ImmediateKind.Index, StackEffectKind.Call,
                    ValidationRule.Call, nameof(Execution.Interpreter.Call))]
                [Instruction(0, 0x1A, "drop", ImmediateKind.None, StackEffectKind.Drop,
                    ValidationRule.Drop, nameof(Execution.Interpreter.Drop))]
                [Instruction(0, 0x20, "local.get", ImmediateKind.Index, StackEffectKind.LocalGet,
                    ValidationRule.LocalGet, nameof(Execution.Interpreter.LocalGet))]
                [Instruction(0, 0x21, "local.set", ImmediateKind.Index, StackEffectKind.LocalSet,
                    ValidationRule.LocalSet, nameof(Execution.Interpreter.LocalSet))]
                [Instruction(0, 0x22, "local.tee", ImmediateKind.Index, StackEffectKind.LocalTee,
                    ValidationRule.LocalTee, nameof(Execution.Interpreter.LocalTee))]
                [Instruction(0, 0x23, "global.get", ImmediateKind.Index, StackEffectKind.GlobalGet,
                    ValidationRule.GlobalGet, nameof(Execution.Interpreter.GlobalGet))]
                [Instruction(0, 0x24, "global.set", ImmediateKind.Index, StackEffectKind.GlobalSet,
                    ValidationRule.GlobalSet, nameof(Execution.Interpreter.GlobalSet))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Unreachable(InterpreterContext context, in Instruction instruction)
                        => ExecutionResult.Trap(WasmSharp.Exceptions.WasmTrapReason.Unreachable,
                            context.FunctionIndex, instruction.ByteOffset);
                    internal static ExecutionResult Return(InterpreterContext context, in Instruction instruction)
                    {
                        context.Log.Add("return");
                        context.CompleteFrame();
                        return default;
                    }
                    internal static ExecutionResult Call(InterpreterContext context, in Instruction instruction)
                        => Record(context, "call", in instruction);
                    internal static ExecutionResult Drop(InterpreterContext context, in Instruction instruction)
                        => Record(context, "drop", in instruction);
                    internal static ExecutionResult LocalGet(InterpreterContext context, in Instruction instruction)
                        => Record(context, "local.get", in instruction);
                    internal static ExecutionResult LocalSet(InterpreterContext context, in Instruction instruction)
                        => Record(context, "local.set", in instruction);
                    internal static ExecutionResult LocalTee(InterpreterContext context, in Instruction instruction)
                        => Record(context, "local.tee", in instruction);
                    internal static ExecutionResult GlobalGet(InterpreterContext context, in Instruction instruction)
                        => Record(context, "global.get", in instruction);
                    internal static ExecutionResult GlobalSet(InterpreterContext context, in Instruction instruction)
                        => Record(context, "global.set", in instruction);
                    private static ExecutionResult Record(
                        InterpreterContext context, string name, in Instruction instruction)
                    {
                        context.Log.Add($"{name} {instruction.Index}");
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
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            static WasmSharp.Execution.Instruction Create(uint code, long offset, uint index)
            {
                WasmSharp.Instructions.InstructionSet.TryGet(new(0, code), out var descriptor);
                return new(descriptor.ExecutionOpcode!.Value, default, offset, index);
            }
            var returned = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [Create(0x20, 100, 3), Create(0x22, 102, 4), Create(0x1A, 104, 0),
                    Create(0x21, 105, uint.MaxValue), Create(0x23, 110, 6), Create(0x24, 112, 7),
                    Create(0x10, 114, 9), Create(0x0F, 116, 0), Create(0x20, 117, 5)]
            };
            var trapped = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [Create(0x00, 12345678901L, 0), Create(0x20, 12345678902L, 5)]
            };
            var returnedResult = WasmSharp.Execution.Interpreter.TestRun(returned);
            var trappedResult = WasmSharp.Execution.Interpreter.TestRun(trapped);
            return System.Linq.Enumerable.SequenceEqual(returned.Log,
                    new[] { "local.get 3", "local.tee 4", "drop 0", "local.set 4294967295",
                        "global.get 6", "global.set 7", "call 9", "return" })
                && returnedResult.Status == WasmSharp.Execution.ExecutionStatus.Success
                && returned.Pc == 8 && returned.FrameCount == 0
                && trapped.Log.Count == 0
                && trappedResult.Status == WasmSharp.Execution.ExecutionStatus.Trap
                && trappedResult.TrapReason == WasmSharp.Exceptions.WasmTrapReason.Unreachable
                && trappedResult.FunctionIndex == 7 && trappedResult.ByteOffset == 12345678901L
                && trapped.Pc == 1 && trapped.FrameCount == 1;
            """
        );
    }

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
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult First(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = instruction.Immediate;
                        context.CompleteFrame();
                        return default;
                    }
                    internal static ExecutionResult Second(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = new(instruction.Immediate.Bits + 1);
                        context.CompleteFrame();
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
            var context = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [new(descriptor.ExecutionOpcode!.Value, new(41), 100, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.Value.Bits == 41;
            """
        );
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, {{code}}), out var descriptor);
            var context = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [new(descriptor.ExecutionOpcode!.Value, new(41), 100, 0)]
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
    public async Task Handlerだけを不正なシグネチャへ変更する_同じdriverで診断が更新される()
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
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)
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
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)
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
        "static void Handler(InterpreterContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "ExecutionResult Handler(InterpreterContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(InterpreterContext context, Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(in InterpreterContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(in Instruction instruction, InterpreterContext context)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler(InterpreterContext context, ref Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ExecutionResult Handler<T>(InterpreterContext context, in Instruction instruction)",
        "WSIG004"
    )]
    [Arguments(
        "static ref ExecutionResult Handler(InterpreterContext context, in Instruction instruction)",
        "WSIG004"
    )]
    public async Task Handlerが存在しないか契約と異なる_属性位置付きのビルドエラーを報告する(
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
    [Arguments("Trap", "WasmSharp.Exceptions.WasmTrapReason.IntegerDivideByZero", "null", "null")]
    [Arguments(
        "Exhaustion",
        "null",
        "WasmSharp.Exceptions.WasmExhaustionReason.CallDepthLimit",
        "12"
    )]
    public async Task Handlerが失敗する_原因と上限と元位置をそのまま返して次の命令を実行しない(
        string status,
        string trapReason,
        string exhaustionReason,
        string limit
    )
    {
        // Arrange
        var failure =
            status == "Trap"
                ? $"ExecutionResult.Trap({trapReason}, context.FunctionIndex, instruction.ByteOffset)"
                : $"ExecutionResult.Exhaustion({exhaustionReason}, {limit}, context.FunctionIndex, instruction.ByteOffset)";
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
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 1);
                    internal static ExecutionResult Fail(InterpreterContext context, in Instruction instruction)
                        => {{failure}};
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
            var context = new WasmSharp.Execution.InterpreterContext(2)
            {
                Instructions = [new(descriptor.ExecutionOpcode!.Value, default, 12345678901L, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return result.Status == WasmSharp.Execution.ExecutionStatus.{{status}}
                && result.TrapReason == {{trapReason}} && result.ExhaustionReason == {{exhaustionReason}}
                && result.Limit == {{limit}} && result.Values.IsEmpty && !result.Values.IsDefault
                && result.FunctionIndex == 7 && result.ByteOffset == 12345678901L
                && context.Pc == 1 && context.FrameCount == 2;
            """
        );
    }

    [Test]
    public async Task Opcodeが重複する_属性位置付きのビルドエラーを報告する()
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
                    internal static ExecutionResult TestRun(InterpreterContext context, int entryFrameCount)
                        => RunLoop(context, entryFrameCount);
                    internal static ExecutionResult PushConstant(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = instruction.Immediate;
                        return default;
                    }
                    internal static ExecutionResult Return(InterpreterContext context, in Instruction instruction)
                    {
                        context.CompleteFrame();
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
            var context = new WasmSharp.Execution.InterpreterContext({{entryFrameCount + 1}})
            {
                Instructions = [new(constant.ExecutionOpcode!.Value, new(42), 100, 0),
                    new(end.ExecutionOpcode!.Value, default, 102, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context, {{entryFrameCount}});
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && result.Values.IsEmpty && !result.Values.IsDefault
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
                    internal static ExecutionResult PushConstant(InterpreterContext context, in Instruction instruction)
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
