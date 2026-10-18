using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal partial class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments("MemoryArgument", "MemoryLoad", 16, 0)]
    [Arguments("MemoryArgument", "MemoryStore", 16, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", 8, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", 4, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", 2, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", 1, 0)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", 1, 16)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", 2, 8)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryStoreLane", 4, 4)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryStoreLane", 8, 2)]
    public async Task Vectorメモリ宣言が完全_アクセス幅とlane規則を生成する(
        string immediate,
        string rule,
        int byteWidth,
        int laneCount
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.memory", ImmediateKind.{{immediate}}, StackEffectKind.{{rule}},
                    ValidationRule.{{rule}}, nameof(Execution.Interpreter.Handler), WasmValueKind.V128,
                    {{byteWidth}}, false, LaneCount = {{laneCount}})]
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
            return WasmSharp.Instructions.InstructionSet.TryGet(new(0xFD, 1), out var descriptor)
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && descriptor.LaneCount == {{laneCount}} && descriptor.NumericSignature == null
                && descriptor.MemoryAccessSignature == new WasmSharp.Instructions.MemoryAccessSignature(
                    WasmSharp.WasmValueKind.V128, {{byteWidth}}, false) && descriptor.ExecutionOpcode.HasValue;
            """
        );
    }

    [Test]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", "V128", 0, false, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", "V128", 3, false, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", "V128", 32, false, 0)]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", "V128", 8, true, 0)]
    [Arguments("MemoryArgument", "MemoryStore", "MemoryStore", "V128", 8, false, 0)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryLoadLane", "V128", 16, false, 1)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryLoadLane", "V128", 2, false, 0)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryLoadLane", "V128", 2, false, 4)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryLoadLane", "I32", 4, false, 4)]
    [Arguments(
        "MemoryArgumentLaneIndex",
        "MemoryStoreLane",
        "MemoryStoreLane",
        "V128",
        1,
        true,
        16
    )]
    [Arguments("MemoryArgument", "MemoryLoadLane", "MemoryLoadLane", "V128", 2, false, 8)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoad", "MemoryLoad", "V128", 2, false, 8)]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryStoreLane", "V128", 2, false, 8)]
    public async Task Vectorメモリの幅やlane規則が不整合_既存のメモリ診断を報告する(
        string immediate,
        string stackEffect,
        string validation,
        string valueType,
        int byteWidth,
        bool signExtend,
        int laneCount
    )
    {
        // Arrange
        var sign = signExtend ? "true" : "false";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0xFD, 1, "simd.memory", ImmediateKind.{{immediate}}, StackEffectKind.{{stackEffect}},
                    ValidationRule.{{validation}}, nameof(Execution.Interpreter.Handler), WasmValueKind.{{valueType}},
                    {{byteWidth}}, {{sign}}, LaneCount = {{laneCount}})]
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
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG005");
    }

    [Test]
    [Arguments(0x30, "i64.load8_s", "MemoryLoad", "I64", 1, true)]
    [Arguments(0x3B, "i32.store16", "MemoryStore", "I32", 2, false)]
    [Arguments(0x2A, "f32.load", "MemoryLoad", "F32", 4, false)]
    [Arguments(0x39, "f64.store", "MemoryStore", "F64", 8, false)]
    public async Task メモリ宣言が完全_アクセス情報と即値を独立してhandlerへ渡す(
        int code,
        string name,
        string rule,
        string valueType,
        int byteWidth,
        bool signExtend
    )
    {
        // Arrange
        var sign = signExtend ? "true" : "false";
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions
            {
                [Instruction(0, {{code}}, "{{name}}", ImmediateKind.MemoryArgument, StackEffectKind.{{rule}},
                    ValidationRule.{{rule}}, nameof(Execution.Interpreter.Access), WasmValueKind.{{valueType}},
                    {{byteWidth}}, {{sign}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Access(InterpreterContext context, in Instruction instruction)
                    {
                        var received = instruction.MemoryArgument.Alignment == uint.MaxValue
                            && instruction.MemoryArgument.Offset == 0xFEDCBA98u
                            && instruction.SecondaryIndex == 0
                            && instruction.MemoryAccessSignature == new Instructions.MemoryAccessSignature(
                                WasmValueKind.{{valueType}}, {{byteWidth}}, {{sign}});
                        context.Value = WasmValue.FromI32(received ? 1 : 0);
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
            var expected = new WasmSharp.Instructions.MemoryAccessSignature(
                WasmSharp.WasmValueKind.{{valueType}}, {{byteWidth}}, {{sign}});
            var attribute = new WasmSharp.Instructions.InstructionAttribute(0, {{code}}, "{{name}}",
                WasmSharp.Instructions.ImmediateKind.MemoryArgument, WasmSharp.Instructions.StackEffectKind.{{rule}},
                WasmSharp.Instructions.ValidationRule.{{rule}}, "Access", WasmSharp.WasmValueKind.{{valueType}},
                {{byteWidth}}, {{sign}});
            var decoded = WasmSharp.Modules.DecodedInstruction.WithMemoryArgument(new(0, {{code}}),
                new(uint.MaxValue, 0xFEDCBA98u), 12345678901L);
            var instruction = new WasmSharp.Execution.Instruction(descriptor.ExecutionOpcode!.Value,
                decoded.Immediate, decoded.ByteOffset, decoded.Index,
                memoryArgument: decoded.MemoryArgument,
                memoryAccessSignature: descriptor.MemoryAccessSignature.GetValueOrDefault());
            var context = new WasmSharp.Execution.InterpreterContext(1) { Instructions = [instruction] };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.MemoryArgument
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && descriptor.MemoryAccessSignature == expected && attribute.MemoryAccessSignature == expected
                && descriptor.NumericSignature == null && attribute.NumericSignature == null
                && decoded.Opcode == new WasmSharp.Instructions.OpcodeKey(0, {{code}})
                && decoded.MemoryArgument.Alignment == uint.MaxValue && decoded.MemoryArgument.Offset == 0xFEDCBA98u
                && decoded.Index == 0 && decoded.SecondaryIndex == 0
                && decoded.Labels.IsEmpty && instruction.TargetCount == 0
                && instruction.ByteOffset == 12345678901L
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Pc == 1 && context.FrameCount == 0 && context.Value.AsI32() == 1;
            """
        );
    }

    [Test]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", "")]
    [Arguments("MemoryArgument", "MemoryStore", "MemoryStore", "")]
    [Arguments("MemoryArgumentLaneIndex", "MemoryLoadLane", "MemoryLoadLane", "")]
    [Arguments("MemoryArgumentLaneIndex", "MemoryStoreLane", "MemoryStoreLane", "")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.I32, 0, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.I32, 8, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.I64, 3, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.F32, 1, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.FuncRef, 4, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.I32, 4, true")]
    [Arguments("MemoryArgument", "MemoryStore", "MemoryStore", ", WasmValueKind.I32, 1, true")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryLoad", ", WasmValueKind.F32, 4, true")]
    [Arguments("None", "None", "None", ", WasmValueKind.I32, 4, false")]
    [Arguments("MemoryArgument", "MemoryLoad", "MemoryStore", ", WasmValueKind.I32, 4, false")]
    [Arguments("None", "MemoryLoad", "MemoryLoad", ", WasmValueKind.I32, 4, false")]
    public async Task メモリアクセス情報が不正_属性位置に診断を報告して生成を中断する(
        string immediate,
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
                [Instruction(0, 0x28, "memory.access", ImmediateKind.{{immediate}}, StackEffectKind.{{stackEffect}},
                    ValidationRule.{{validation}}, nameof(Execution.Interpreter.Access){{signature}})]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult Access(InterpreterContext context, in Instruction instruction)
                        => default;
                }
            }
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG005");
    }
}
