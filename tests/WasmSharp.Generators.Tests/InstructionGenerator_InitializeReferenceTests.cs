using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal partial class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments(0, 0xD1, "ref.is_null", "None", "RefIsNull")]
    [Arguments(0, 0xD2, "ref.func", "Index", "RefFunc")]
    [Arguments(0, 0x25, "table.get", "Index", "TableGet")]
    [Arguments(0, 0x26, "table.set", "Index", "TableSet")]
    [Arguments(0xFC, 0x0C, "table.init", "IndexPair", "TableInit")]
    [Arguments(0xFC, 0x0D, "elem.drop", "Index", "ElementDrop")]
    [Arguments(0xFC, 0x0E, "table.copy", "IndexPair", "TableCopy")]
    [Arguments(0xFC, 0x0F, "table.grow", "Index", "TableGrow")]
    [Arguments(0xFC, 0x10, "table.size", "Index", "TableSize")]
    [Arguments(0xFC, 0x11, "table.fill", "Index", "TableFill")]
    [Arguments(0, 0x11, "call_indirect", "IndexPair", "CallIndirect")]
    public async Task 参照とテーブルの命令を宣言する_規則と独立した添字を生成handlerへ渡す(
        int prefix,
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
                [Instruction({{prefix}}, {{code}}, "{{name}}", ImmediateKind.{{immediate}},
                    StackEffectKind.{{rule}}, ValidationRule.{{rule}}, nameof(Execution.Interpreter.Receive))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Receive(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = WasmValue.FromI64((long)instruction.Index + instruction.SecondaryIndex);
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
            var opcode = new WasmSharp.Instructions.OpcodeKey({{prefix}}, {{code}});
            var found = WasmSharp.Instructions.InstructionSet.TryGet(opcode, out var descriptor);
            var decoded = "{{immediate}}" == "IndexPair"
                ? WasmSharp.Modules.DecodedInstruction.WithIndexPair(opcode, uint.MaxValue, 0xFEDCBA98u, 12345678901L)
                : "{{immediate}}" == "Index"
                    ? WasmSharp.Modules.DecodedInstruction.WithIndex(opcode, uint.MaxValue, 12345678901L)
                    : WasmSharp.Modules.DecodedInstruction.WithoutImmediate(opcode, 12345678901L);
            var expectedIndex = "{{immediate}}" == "None" ? 0u : uint.MaxValue;
            var expectedSecondaryIndex = "{{immediate}}" == "IndexPair" ? 0xFEDCBA98u : 0u;
            var instruction = new WasmSharp.Execution.Instruction(descriptor.ExecutionOpcode!.Value,
                decoded.Immediate, decoded.ByteOffset, decoded.Index, secondaryIndex: decoded.SecondaryIndex);
            var context = new WasmSharp.Execution.InterpreterContext(1) { Instructions = [instruction] };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "{{name}}"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.{{immediate}}
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.{{rule}}
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.{{rule}}
                && descriptor.NumericSignature == null && descriptor.MemoryAccessSignature == null
                && decoded.Opcode == opcode && decoded.ByteOffset == 12345678901L
                && decoded.Index == expectedIndex && decoded.SecondaryIndex == expectedSecondaryIndex
                && decoded.Immediate.Equals(default(WasmSharp.WasmValue))
                && !decoded.Labels.IsDefault && decoded.Labels.IsEmpty
                && !decoded.SelectTypes.IsDefault && decoded.SelectTypes.IsEmpty
                && decoded.MemoryArgument == default && decoded.BlockType.Equals(default(WasmSharp.Modules.BlockType))
                && instruction.Index == expectedIndex && instruction.SecondaryIndex == expectedSecondaryIndex
                && instruction.TargetCount == 0 && instruction.MemoryArgument == default
                && instruction.MemoryAccessSignature == default && instruction.ByteOffset == decoded.ByteOffset
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Value.AsI64() == (long)expectedIndex + expectedSecondaryIndex
                && context.Pc == 1 && context.FrameCount == 0
                && context.Log.Count == 1 && context.Log[0] == "{{name}}";
            """
        );
    }

    [Test]
    [Arguments("FuncRef")]
    [Arguments("ExternRef")]
    public async Task 参照型即値の命令を宣言する_定数規則を再利用して型別nullを生成handlerへ渡す(
        string referenceType
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(0, 0xD0, "ref.null", ImmediateKind.ReferenceType,
                    StackEffectKind.PushReference, ValidationRule.Constant, nameof(Execution.Interpreter.Receive))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult Receive(InterpreterContext context, in Instruction instruction)
                    {
                        context.Value = instruction.Immediate;
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
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0xD0), out var descriptor);
            var value = WasmSharp.WasmValue.From{{referenceType}}(null);
            var decoded = WasmSharp.Modules.DecodedInstruction.WithImmediate(new(0, 0xD0), value, 12345678901L);
            var instruction = new WasmSharp.Execution.Instruction(descriptor.ExecutionOpcode!.Value,
                decoded.Immediate, decoded.ByteOffset, decoded.Index);
            var context = new WasmSharp.Execution.InterpreterContext(1) { Instructions = [instruction] };
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return found && descriptor.Name == "ref.null"
                && descriptor.Immediate == WasmSharp.Instructions.ImmediateKind.ReferenceType
                && descriptor.StackEffect == WasmSharp.Instructions.StackEffectKind.PushReference
                && descriptor.Validation == WasmSharp.Instructions.ValidationRule.Constant
                && descriptor.NumericSignature == null && descriptor.MemoryAccessSignature == null
                && decoded.Immediate.Kind == WasmSharp.WasmValueKind.{{referenceType}}
                && decoded.Immediate.As{{referenceType}}() == null
                && decoded.Index == 0 && decoded.SecondaryIndex == 0
                && decoded.Labels.IsEmpty && decoded.SelectTypes.IsEmpty && decoded.MemoryArgument == default
                && instruction.Index == 0 && instruction.SecondaryIndex == 0 && instruction.TargetCount == 0
                && instruction.MemoryArgument == default && instruction.ByteOffset == decoded.ByteOffset
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success
                && context.Value.Kind == WasmSharp.WasmValueKind.{{referenceType}}
                && context.Value.As{{referenceType}}() == null
                && context.Pc == 1 && context.FrameCount == 0;
            """
        );
    }
}
