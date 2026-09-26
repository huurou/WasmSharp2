using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_GlobalGetTests
{
    [Test]
    public async Task ホストが更新したglobalを取得する_所属instanceの同じ実体から型とビット列と参照同一性を保って積む()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x23), out var descriptor);
        var externGlobal = new WasmGlobal(
            new(WasmValueKind.ExternRef, true),
            WasmValue.FromExternRef(null)
        );
        var vectorGlobal = new WasmGlobal(
            new(WasmValueKind.V128, false),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210)
        );
        var numberGlobal = new WasmGlobal(new(WasmValueKind.F32, true), WasmValue.FromF32Bits(0));
        var instance = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.ExternRef, WasmValueKind.V128, WasmValueKind.F32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x23, 1001, 0),
                        InstructionFixture.Create(0x23, 1003, 1),
                        InstructionFixture.Create(0x23, 1005, 2),
                        InstructionFixture.Create(0x0B, 1007),
                    ],
                    3
                ),
            ],
            [],
            [externGlobal, vectorGlobal, numberGlobal]
        );
        externGlobal.Value = WasmValue.FromExternRef(reference);
        numberGlobal.Value = WasmValue.FromF32Bits(0x7FC00001);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[0],
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
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.Index);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.GlobalGet);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.GlobalGet);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(3);
            await Assert.That(result.Values[0].AsExternRef()).IsSameReferenceAs(reference);
            await Assert
                .That(result.Values[1].AsV128())
                .IsEqualTo((0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL));
            await Assert.That(result.Values[2].AsF32Bits()).IsEqualTo(0x7FC00001U);
        }
    }

    [Test]
    public async Task Importした定義関数から取得する_import先ではなく元instanceのglobalを積む()
    {
        // Arrange
        var source = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.I32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x23, 1001, 0),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    1
                ),
            ],
            [],
            [new(new(WasmValueKind.I32, false), WasmValue.FromI32(1))]
        );
        var importer = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.I32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 1001, 0),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    1
                ),
            ],
            [source.Functions[0]],
            [new(new(WasmValueKind.I32, false), WasmValue.FromI32(2))]
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
            await Assert.That(result.Values.Single().AsI32()).IsEqualTo(1);
        }
    }
}
