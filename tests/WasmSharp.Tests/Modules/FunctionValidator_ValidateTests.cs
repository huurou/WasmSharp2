using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    public async Task 解決済みの型でcallとlocalsとglobalとreturnを検査_入力とmodule状態を変えず線形化する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7E]), ([0x7F, 0x7E], [0x7E, 0x7F])),
                HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 1]), ("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Globals((0x7E, true, [0x42, 0, 0x0B])),
                HostLinkingModuleBinary.Exports(("run", 0, 1)),
                HostLinkingModuleBinary.Code(
                    ([(1, 0x7E)], Convert.FromHexString("200024002301210220001000220223000F1A0B"))
                )
            )
        );
        WasmFunctionType[] functionTypes = [module.Types[0], module.Types[1]];
        WasmGlobalType[] globalTypes = [new(WasmValueKind.I32, true), module.Globals[0].Type];
        var originalFunctionTypes = functionTypes.ToArray();
        var originalGlobalTypes = globalTypes.ToArray();
        var definition = module.Functions[0];

        // Act
        var code = FunctionValidator.Validate(module, 0, 1, functionTypes, globalTypes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.LocalCount).IsEqualTo(1UL);
            await Assert.That(code.Locals[0].Count).IsEqualTo(1u);
            await Assert.That(code.Locals[0].Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(code.MaxOperandStack).IsEqualTo(2);
            await Assert.That(code.Instructions.Length).IsEqualTo(definition.Instructions.Length);
            for (var index = 0; index < code.Instructions.Length; index++)
            {
                var decoded = definition.Instructions[index];
                InstructionSet.TryGet(decoded.Opcode, out var descriptor);
                await Assert
                    .That(code.Instructions[index].Opcode)
                    .IsEqualTo(descriptor.ExecutionOpcode);
                await Assert.That(code.Instructions[index].Index).IsEqualTo(decoded.Index);
                await Assert
                    .That(code.Instructions[index].ByteOffset)
                    .IsEqualTo(decoded.ByteOffset);
            }
            for (var index = 0; index < functionTypes.Length; index++)
            {
                await Assert
                    .That(functionTypes[index])
                    .IsSameReferenceAs(originalFunctionTypes[index]);
            }
            for (var index = 0; index < globalTypes.Length; index++)
            {
                await Assert.That(globalTypes[index]).IsSameReferenceAs(originalGlobalTypes[index]);
            }
            await Assert.That(module.Functions[0]).IsSameReferenceAs(definition);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 到達不能部分に具体型の不一致がある_入力とmodule状態を変えず元関数位置で拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0x00, 0x42, 1, 0x0B]))
            )
        );
        WasmFunctionType[] functionTypes = [module.Types[0], module.Types[0]];
        WasmGlobalType[] globalTypes = [];
        var definition = module.Functions[0];

        // Act & Assert
        var exception = await Assert
            .That(() => FunctionValidator.Validate(module, 0, 1, functionTypes, globalTypes))
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, definition.Instructions[^1].ByteOffset, 1, 10)
                );
            await Assert.That(functionTypes[0]).IsSameReferenceAs(module.Types[0]);
            await Assert.That(functionTypes[1]).IsSameReferenceAs(module.Types[0]);
            await Assert.That(globalTypes.Length).IsEqualTo(0);
            await Assert.That(module.Functions[0]).IsSameReferenceAs(definition);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
