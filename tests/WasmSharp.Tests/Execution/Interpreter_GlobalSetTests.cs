using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_GlobalSetTests
{
    [Test]
    public async Task Mutableなglobalへ設定する_最上位の値を除き同じ実体からホストと後続の取得で更新後の値を得る()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x24), out var descriptor);
        var numberGlobal = new WasmGlobal(new(WasmValueKind.F64, true), WasmValue.FromF64Bits(0));
        var externGlobal = new WasmGlobal(
            new(WasmValueKind.ExternRef, true),
            WasmValue.FromExternRef(null)
        );
        var instance = ExecutionInstanceFixture.Create(
            [
                new(
                    [WasmValueKind.F64, WasmValueKind.ExternRef],
                    [WasmValueKind.I32, WasmValueKind.F64]
                ),
            ],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x41, 1001, immediate: WasmValue.FromI32(7)),
                        InstructionFixture.Create(0x20, 1003, 0),
                        InstructionFixture.Create(0x24, 1005, 0),
                        InstructionFixture.Create(0x20, 1007, 1),
                        InstructionFixture.Create(0x24, 1009, 1),
                        InstructionFixture.Create(0x23, 1011, 0),
                        InstructionFixture.Create(0x0B, 1013),
                    ],
                    2
                ),
            ],
            [],
            [numberGlobal, externGlobal]
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[0],
                [WasmValue.FromF64Bits(0xFFF4000000000001), WasmValue.FromExternRef(reference)],
                WasmProcessingStage.Invoke
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.Index);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.GlobalSet);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.GlobalSet);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(2);
            await Assert.That(result.Values[0]).IsEqualTo(WasmValue.FromI32(7));
            await Assert.That(result.Values[1].AsF64Bits()).IsEqualTo(0xFFF4000000000001UL);
            await Assert.That(numberGlobal.Value.AsF64Bits()).IsEqualTo(0xFFF4000000000001UL);
            await Assert.That(externGlobal.Value.AsExternRef()).IsSameReferenceAs(reference);
        }
    }

    [Test]
    public async Task Importした定義関数から設定する_元instanceのglobalだけを更新する()
    {
        // Arrange
        var sourceGlobal = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var importerGlobal = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var source = ExecutionInstanceFixture.Create(
            [new([WasmValueKind.I32], [])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x20, 1001, 0),
                        InstructionFixture.Create(0x24, 1003, 0),
                        InstructionFixture.Create(0x0B, 1005),
                    ],
                    1
                ),
            ],
            [],
            [sourceGlobal]
        );
        var importer = ExecutionInstanceFixture.Create(
            [new([], [])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x41, 1001, immediate: WasmValue.FromI32(42)),
                        InstructionFixture.Create(0x10, 1003, 0),
                        InstructionFixture.Create(0x0B, 1005),
                    ],
                    1
                ),
            ],
            [source.Functions[0]],
            [importerGlobal]
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)importer.Functions[1],
                [],
                WasmProcessingStage.Invoke
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(sourceGlobal.Value.AsI32()).IsEqualTo(42);
            await Assert.That(importerGlobal.Value.AsI32()).IsEqualTo(0);
        }
    }
}
