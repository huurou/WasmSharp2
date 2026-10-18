using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal partial class InstructionGenerator_InitializeTests
{
    [Test]
    public async Task LaneCountを重複指定する_生成器例外を起こさず不完全宣言の診断を報告する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.invalid", ImmediateKind.LaneIndex, StackEffectKind.Numeric,
                    ValidationRule.Numeric, nameof(Execution.Interpreter.Handler),
                    new WasmValueKind[] { WasmValueKind.V128 }, WasmValueKind.F32,
                    LaneCount = 4, LaneCount = 2)]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) => default;
                }
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
        await Assert.That(driver.GetRunResult().Results.Single().Exception).IsNull();
    }

    [Test]
    [Arguments("V128", "PushI32", "Constant")]
    [Arguments("None", "PushV128", "Constant")]
    [Arguments("V128", "PushV128", "None")]
    [Arguments("LaneIndex", "PushI32", "Constant")]
    [Arguments("Shuffle", "PushI32", "Constant")]
    public async Task SIMD即値と型効果が不整合_既存の不完全宣言診断を報告する(
        string immediate,
        string stackEffect,
        string validation
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.invalid", ImmediateKind.{{immediate}}, StackEffectKind.{{stackEffect}},
                    ValidationRule.{{validation}}, nameof(Execution.Interpreter.Handler))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) => default;
                }
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
    [Arguments("WasmValueKind.I32", "V128", "None", 0)]
    [Arguments("WasmValueKind.V128, WasmValueKind.I32", "V128", "None", 0)]
    [Arguments("WasmValueKind.V128, WasmValueKind.F32", "V128", "LaneIndex", 4)]
    [Arguments("WasmValueKind.V128", "I32", "LaneIndex", 16)]
    [Arguments("WasmValueKind.V128", "I32", "LaneIndex", 8)]
    [Arguments("WasmValueKind.V128", "F64", "LaneIndex", 2)]
    [Arguments("WasmValueKind.V128, WasmValueKind.V128", "V128", "Shuffle", 0)]
    [Arguments("WasmValueKind.V128, WasmValueKind.V128, WasmValueKind.V128", "V128", "None", 0)]
    public async Task SIMD数値宣言が完全_型順と即値形状とlane上限を生成して入力配列をコピーする(
        string inputTypes,
        string resultType,
        string immediate,
        int laneCount
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.numeric", ImmediateKind.{{immediate}}, StackEffectKind.Numeric,
                    ValidationRule.Numeric, nameof(Execution.Interpreter.Handler),
                    new WasmValueKind[] { {{inputTypes}} }, WasmValueKind.{{resultType}}, LaneCount = {{laneCount}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) => default;
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
            var inputs = new WasmSharp.WasmValueKind[] { {{inputTypes.Replace(
                "WasmValueKind",
                "WasmSharp.WasmValueKind"
            )}} };
            var expected = System.Linq.Enumerable.ToArray(inputs);
            var attribute = new WasmSharp.Instructions.InstructionAttribute(0xFD, 1, "simd.numeric",
                WasmSharp.Instructions.ImmediateKind.{{immediate}}, WasmSharp.Instructions.StackEffectKind.Numeric,
                WasmSharp.Instructions.ValidationRule.Numeric, "Handler", inputs, WasmSharp.WasmValueKind.{{resultType}})
                { LaneCount = {{laneCount}} };
            inputs[0] = WasmSharp.WasmValueKind.ExternRef;
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, 1), out var descriptor)
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.LaneCount == {{laneCount}} && attribute.LaneCount == {{laneCount}}
                && descriptor.NumericSignature is { } generated && attribute.NumericSignature is { } owned
                && System.Linq.Enumerable.SequenceEqual(generated.InputTypes, expected)
                && System.Linq.Enumerable.SequenceEqual(owned.InputTypes, expected)
                && generated.ResultType == WasmSharp.WasmValueKind.{{resultType}}
                && owned.ResultType == generated.ResultType && descriptor.ExecutionOpcode.HasValue;
            """
        );
    }

    [Test]
    public async Task V128定数を宣言する_定数即値と型効果を生成する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 12, "v128.const", ImmediateKind.V128, StackEffectKind.PushV128,
                    ValidationRule.Constant, nameof(Execution.Interpreter.Handler))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) => default;
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
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, 12), out var descriptor)
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.V128
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.PushV128
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.Constant
                && descriptor.NumericSignature == null && descriptor.MemoryAccessSignature == null
                && descriptor.LaneCount == 0 && descriptor.ExecutionOpcode.HasValue;
            """
        );
    }

    [Test]
    [Arguments("new WasmValueKind[] { }", "I32", "None", 0)]
    [Arguments("null", "I32", "None", 0)]
    [Arguments(
        "new WasmValueKind[] { WasmValueKind.I32, WasmValueKind.I32, WasmValueKind.I32, WasmValueKind.I32 }",
        "I32",
        "None",
        0
    )]
    [Arguments("new WasmValueKind[] { WasmValueKind.FuncRef }", "V128", "None", 0)]
    [Arguments("new WasmValueKind[] { (WasmValueKind)999 }", "V128", "None", 0)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "ExternRef", "None", 0)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "I32", "LaneIndex", 0)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "I32", "LaneIndex", 3)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "F64", "LaneIndex", 4)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "I32", "None", 16)]
    [Arguments("new WasmValueKind[] { WasmValueKind.V128 }", "V128", "Shuffle", 0)]
    [Arguments(
        "new WasmValueKind[] { WasmValueKind.V128, WasmValueKind.I32 }",
        "V128",
        "Shuffle",
        0
    )]
    public async Task SIMD型列やlane制約が不正_既存の不完全宣言診断を報告する(
        string inputs,
        string resultType,
        string immediate,
        int laneCount
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.invalid", ImmediateKind.{{immediate}}, StackEffectKind.Numeric,
                    ValidationRule.Numeric, nameof(Execution.Interpreter.Handler), {{inputs}},
                    WasmValueKind.{{resultType}}, LaneCount = {{laneCount}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) => default;
                }
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
    [Arguments(0, 0x79, "i64.clz", 1, "I64", "I64")]
    [Arguments(0, 0x50, "i64.eqz", 1, "I64", "I32")]
    [Arguments(0, 0x51, "i64.eq", 2, "I64", "I32")]
    [Arguments(0, 0x6A, "i32.add", 2, "I32", "I32")]
    [Arguments(0, 0x8D, "f32.ceil", 1, "F32", "F32")]
    [Arguments(0, 0xA0, "f64.add", 2, "F64", "F64")]
    [Arguments(0xFC, 0, "i32.trunc_sat_f32_s", 1, "F32", "I32")]
    [Arguments(0xFD, 82, "v128.bitselect", 3, "V128", "V128")]
    [Arguments(0xFD, 83, "v128.any_true", 1, "V128", "I32")]
    public async Task 数値宣言が完全_型効果を記述子へ反映して宣言したhandlerを直接実行する(
        int prefix,
        int code,
        string name,
        int inputCount,
        string inputType,
        string resultType
    )
    {
        // Arrange
        var inputTypes = string.Join(
            ", ",
            Enumerable.Repeat($"WasmSharp.WasmValueKind.{inputType}", inputCount)
        );
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction({{prefix}}, {{code}}, "{{name}}", ImmediateKind.None, StackEffectKind.Numeric,
                    ValidationRule.Numeric, nameof(Execution.Interpreter.Numeric), {{inputCount}},
                    WasmValueKind.{{inputType}}, WasmValueKind.{{resultType}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Numeric(InterpreterContext context, in Instruction instruction)
                    {
                        context.Log.Add("{{name}}");
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
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new({{prefix}}, {{code}}), out var descriptor);
            var expected = new WasmSharp.Instructions.NumericSignature([{{inputTypes}}],
                WasmSharp.WasmValueKind.{{resultType}});
            var attribute = new WasmSharp.Instructions.InstructionAttribute({{prefix}}, {{code}}, "{{name}}",
                WasmSharp.Instructions.ImmediateKind.None, WasmSharp.Instructions.StackEffectKind.Numeric,
                WasmSharp.Instructions.ValidationRule.Numeric, "Numeric", {{inputCount}},
                WasmSharp.WasmValueKind.{{inputType}}, WasmSharp.WasmValueKind.{{resultType}});
            var context = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [new(descriptor.ExecutionOpcode!.Value, default, 12345678901L, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.None
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.Numeric
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.Numeric
                && descriptor.NumericSignature is { } generatedSignature && attribute.NumericSignature is { } attributeSignature
                && System.Linq.Enumerable.SequenceEqual(generatedSignature.InputTypes, expected.InputTypes)
                && generatedSignature.ResultType == expected.ResultType
                && System.Linq.Enumerable.SequenceEqual(attributeSignature.InputTypes, expected.InputTypes)
                && attributeSignature.ResultType == expected.ResultType
                && descriptor.MemoryAccessSignature == null && attribute.MemoryAccessSignature == null
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Pc == 1 && context.FrameCount == 0
                && context.Log.Count == 1 && context.Log[0] == "{{name}}";
            """
        );
    }

    [Test]
    [Arguments("Numeric", "Numeric", "")]
    [Arguments("Numeric", "Constant", "")]
    [Arguments("PushI32", "Numeric", "")]
    [Arguments("PushI32", "Constant", ", 1, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments("Numeric", "Constant", ", 1, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments("PushI32", "Numeric", ", 1, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 0, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 4, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.FuncRef, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.ExternRef, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.ExnRef, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 1, (WasmValueKind)999, WasmValueKind.I32")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.I32, WasmValueKind.FuncRef")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.I32, WasmValueKind.ExternRef")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.I32, WasmValueKind.ExnRef")]
    [Arguments("Numeric", "Numeric", ", 1, WasmValueKind.I32, (WasmValueKind)999")]
    public async Task 数値規則とsignatureが不整合_不完全宣言の診断を属性位置に報告する(
        string stackEffect,
        string validation,
        string signature
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x6A, "i32.add", ImmediateKind.None, StackEffectKind.{{stackEffect}},
                    ValidationRule.{{validation}}, nameof(Execution.Interpreter.Handler){{signature}})]
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
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG002");
    }

    [Test]
    [Arguments("", "WSIG003", "2, WasmValueKind.I32, WasmValueKind.I32")]
    [Arguments(
        "",
        "WSIG003",
        "new WasmValueKind[] { WasmValueKind.V128, WasmValueKind.I32 }, WasmValueKind.V128"
    )]
    [Arguments(
        "internal static int Handler(InterpreterContext context, in Instruction instruction) => 0;",
        "WSIG004",
        "2, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "internal static int Handler(InterpreterContext context, in Instruction instruction) => 0;",
        "WSIG004",
        "new WasmValueKind[] { WasmValueKind.V128, WasmValueKind.I32 }, WasmValueKind.V128"
    )]
    public async Task 数値宣言のhandlerが契約に合わない_既存のhandler診断を報告する(
        string handler,
        string expectedId,
        string signature
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x6A, "i32.add", ImmediateKind.None, StackEffectKind.Numeric,
                    ValidationRule.Numeric, "Handler", {{signature}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    {{handler}}
                }
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, expectedId);
    }

    [Test]
    [Arguments(0x02, "block", "BlockType", "Block")]
    [Arguments(0x0E, "br_table", "BranchTable", "BranchTable")]
    [Arguments(0x1C, "select", "TypeVector", "TypedSelect")]
    public async Task 複合即値と制御規則を宣言する_数値signatureなしで記述子へ反映する(
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
                [Instruction(0, {{code}}, "{{name}}", ImmediateKind.{{immediate}}, StackEffectKind.{{rule}},
                    ValidationRule.{{rule}}, nameof(Execution.Interpreter.Handler))]
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
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0, {{code}}), out var descriptor)
                && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && descriptor.ExecutionOpcode.HasValue && descriptor.NumericSignature == null;
            """
        );
    }

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
                        context.Value = WasmValue.FromI64(instruction.Immediate.AsI64() + instruction.Index);
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
            var decoded = WasmSharp.Modules.DecodedInstruction.WithImmediate(new(0, {{code}}), WasmSharp.WasmValue.FromI64(42), 12345678901L);
            var indexed = WasmSharp.Modules.DecodedInstruction.WithIndex(new(0, {{code}}), uint.MaxValue, decoded.ByteOffset);
            var instruction = new WasmSharp.Execution.Instruction(descriptor.ExecutionOpcode!.Value,
                decoded.Immediate, decoded.ByteOffset, indexed.Index);
            var context = new WasmSharp.Execution.InterpreterContext(1) { Instructions = [instruction] };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && decoded.Opcode == new WasmSharp.Instructions.OpcodeKey(0, {{code}})
                && decoded.Index == 0 && indexed.Index == uint.MaxValue && instruction.Index == uint.MaxValue
                && decoded.Immediate.AsI64() == 42 && instruction.Immediate.AsI64() == 42
                && instruction.ByteOffset == 12345678901L
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Value.AsI64() == 4294967337L;
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
                        context.Value = WasmValue.FromI64(instruction.Immediate.AsI64() + 1);
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
                Instructions = [new(descriptor.ExecutionOpcode!.Value, WasmSharp.WasmValue.FromI64(41), 100, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.Value.AsI64() == 41;
            """
        );
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, {{code}}), out var descriptor);
            var context = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [new(descriptor.ExecutionOpcode!.Value, WasmSharp.WasmValue.FromI64(41), 100, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{stackEffect}}
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0x41), out _)
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.Value.AsI64() == 42;
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
    [Arguments(
        "ImmediateKind.Unsupported, StackEffectKind.Numeric, ValidationRule.Numeric, \"Handler\", 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "ImmediateKind.None, StackEffectKind.Unsupported, ValidationRule.Numeric, \"Handler\", 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Unsupported, \"Handler\", 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Numeric, null, 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Numeric, \"\", 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
    [Arguments(
        "(ImmediateKind)999, StackEffectKind.Numeric, ValidationRule.Numeric, \"Handler\", 1, WasmValueKind.I32, WasmValueKind.I32"
    )]
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
    [Arguments(
        "Trap",
        "WasmSharp.Exceptions.WasmTrapReason.IntegerDivideByZero",
        "null",
        "null",
        false
    )]
    [Arguments(
        "Trap",
        "WasmSharp.Exceptions.WasmTrapReason.IntegerDivideByZero",
        "null",
        "null",
        true
    )]
    [Arguments(
        "Exhaustion",
        "null",
        "WasmSharp.Exceptions.WasmExhaustionReason.CallDepthLimit",
        "12",
        false
    )]
    [Arguments(
        "Exhaustion",
        "null",
        "WasmSharp.Exceptions.WasmExhaustionReason.CallDepthLimit",
        "12",
        true
    )]
    public async Task Handlerが失敗する_原因と上限と元位置をそのまま返して次の命令を実行しない(
        string status,
        string trapReason,
        string exhaustionReason,
        string limit,
        bool numeric
    )
    {
        // Arrange
        var failure =
            status == "Trap"
                ? $"ExecutionResult.Trap({trapReason}, context.FunctionIndex, instruction.ByteOffset)"
                : $"ExecutionResult.Exhaustion({exhaustionReason}, {limit}, context.FunctionIndex, instruction.ByteOffset)";
        var metadata = numeric
            ? "ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Numeric, nameof(Execution.Interpreter.Fail), 2, WasmValueKind.I32, WasmValueKind.I32"
            : "ImmediateKind.I32, StackEffectKind.PushI32, ValidationRule.Constant, nameof(Execution.Interpreter.Fail)";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0x41, "test.failure", {{metadata}})]
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
    [Arguments(false)]
    [Arguments(true)]
    public async Task Opcodeが重複する_属性位置付きのビルドエラーを報告する(bool numeric)
    {
        // Arrange
        var metadata = numeric
            ? ", ImmediateKind.None, StackEffectKind.Numeric, ValidationRule.Numeric, nameof(Execution.Interpreter.Handler), 1, WasmValueKind.I32, WasmValueKind.I32"
            : "";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "first"{{metadata}})]
                [Instruction(0xFD, 1, "second"{{metadata}})]
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
                Instructions = [new(constant.ExecutionOpcode!.Value, WasmSharp.WasmValue.FromI64(42), 100, 0),
                    new(end.ExecutionOpcode!.Value, default, 102, 0)]
            };
            var result = WasmSharp.Execution.Interpreter.TestRun(context, {{entryFrameCount}});
            return result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && result.Values.IsEmpty && !result.Values.IsDefault
                && context.Value.AsI64() == 42 && context.Pc == 2 && context.FrameCount == {{entryFrameCount}};
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
                && constant.NumericSignature == null
                && constant.ExecutionOpcode.HasValue
                && unsupportedFound && unsupported.Name == "i32.trunc_sat_f32_s"
                && unsupported.Immediate == WasmSharp.Instructions.ImmediateKind.Unsupported
                && unsupported.StackEffect == WasmSharp.Instructions.StackEffectKind.Unsupported
                && unsupported.Validation == WasmSharp.Instructions.ValidationRule.Unsupported
                && unsupported.ExecutionOpcode == null
                && unsupported.NumericSignature == null
                && System.Enum.GetValues<WasmSharp.Instructions.ExecutionOpcode>().Length == 1
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0), out _)
                && !WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, 0), out _);
            """
        );
    }
}
