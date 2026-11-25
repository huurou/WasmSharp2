using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal partial class InstructionGenerator_InitializeTests
{
    [Test]
    [Arguments("Missing", "ConstantReference", "WSIG003")]
    [Arguments("InputType", "ConstantReference", "WSIG004")]
    [Arguments("RefI31", "None", "WSIG002")]
    public async Task 単項参照演算の名前またはスタック効果が不正である_命令の宣言を診断する(
        string operation,
        string stackEffect,
        string expectedDiagnostic
    )
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            $$"""
            namespace WasmSharp.Instructions;
            [Instruction(WasmSpecVersion.Core3, 0xFB, 1, "make.ref", ImmediateKind.None,
                StackEffectKind.{{stackEffect}}, ValidationRule.ConstantReference, "{{operation}}")]
            internal static partial class InstructionSet;
            """,
            GeneratorTestSource.EXECUTION_CONTRACTS
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, expectedDiagnostic);
    }

    [Test]
    public async Task 演算プロパティのnull許容注釈が異なる_明示的にnullを許容する宣言だけを拒否する()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions;
            internal sealed partial class ConstantReferenceOperation
            {
                internal static ConstantReferenceOperation? Nullable => null;
            #nullable disable annotations
                internal static ConstantReferenceOperation Oblivious => new ConstantReferenceOperation();
            #nullable restore annotations
            }
            [Instruction(WasmSpecVersion.Core3, 0xFB, 1, "nullable.ref", ImmediateKind.None,
                StackEffectKind.ConstantReference, ValidationRule.ConstantReference, nameof(ConstantReferenceOperation.Nullable))]
            [Instruction(WasmSpecVersion.Core3, 0xFB, 2, "oblivious.ref", ImmediateKind.None,
                StackEffectKind.ConstantReference, ValidationRule.ConstantReference, nameof(ConstantReferenceOperation.Oblivious))]
            internal static partial class InstructionSet;
            """
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InstructionGenerator());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Assert
        await GeneratorTestSource.AssertDiagnostic(driver, diagnostics, "WSIG004");
        await Assert.That(diagnostics[0].GetMessage()).Contains("nullable.ref");
    }

    [Test]
    public async Task 単項参照演算を命令に割り当てる_定数式の記述子と関数の実行分岐が同じ演算を使う()
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(WasmSpecVersion.Core3, 0xFB, 1, "make.ref", ImmediateKind.None,
                    StackEffectKind.ConstantReference, ValidationRule.ConstantReference, nameof(ConstantReferenceOperation.RefI31))]
                [Instruction(WasmSpecVersion.Core3, 0, 0x0B, "end", ImmediateKind.None,
                    StackEffectKind.FunctionEnd, ValidationRule.FunctionEnd, nameof(Execution.Interpreter.End))]
                internal static partial class InstructionSet;
            }
            namespace WasmSharp.Execution
            {
                internal static partial class Interpreter
                {
                    internal static ExecutionResult TestRun(InterpreterContext context) => RunLoop(context, 0);
                    internal static ExecutionResult End(InterpreterContext context, in Instruction instruction)
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            """
            var table = WasmSharp.Instructions.InstructionSet.ForVersion(WasmSharp.WasmSpecVersion.Core3);
            var makeReference = table[new(0xFB, 1)];
            var end = table[new(0, 0x0B)];
            var context = new WasmSharp.Execution.InterpreterContext(1)
            {
                Instructions = [new(makeReference.ExecutionOpcode!.Value, default, 0, 0), new(end.ExecutionOpcode!.Value, default, 0, 0)]
            };
            var input = WasmSharp.WasmValue.FromI32(-1);
            context.PushValue(input);
            var result = WasmSharp.Execution.Interpreter.TestRun(context);
            return makeReference.ConstantReferenceOperation!.Evaluate(input).AsI31Unsigned() == int.MaxValue
                && result.Status == WasmSharp.Execution.ExecutionStatus.Success && context.PopValue().AsI31Unsigned() == int.MaxValue;
            """
        );
    }

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
                [Instruction(WasmSpecVersion.Core2, {{prefix}}, {{code}}, "{{name}}", ImmediateKind.{{immediate}},
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var opcode = new WasmSharp.Instructions.OpcodeKey({{prefix}}, {{code}});
            var found = WasmSharp.Instructions.InstructionSet.TryGet(opcode, WasmSharp.WasmSpecVersion.Core2, out var descriptor);
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
    public async Task 参照型即値の命令を宣言する_定数規則を再利用して型別nullを生成handlerへ渡す(string referenceType)
    {
        // Arrange
        var compilation = GeneratorTestSource.CreateCompilation(
            """
            namespace WasmSharp.Instructions
            {
                [Instruction(WasmSpecVersion.Core2, 0, 0xD0, "ref.null", ImmediateKind.ReferenceType,
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        // Assert
        await Assert.That(diagnostics).IsEmpty();
        await GeneratorTestSource.AssertCompilesAndRuns(
            output,
            $$"""
            var found = WasmSharp.Instructions.InstructionSet.TryGet(new(0, 0xD0), WasmSharp.WasmSpecVersion.Core2, out var descriptor);
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
