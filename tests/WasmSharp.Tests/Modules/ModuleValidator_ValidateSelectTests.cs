using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task V128のselect後に余剰値がある_終端位置で型不一致を返し部分コードを公開しない(
        bool typed
    )
    {
        // Arrange
        byte[] select = typed ? [0x1C, 1, 0x7B] : [0x1B];
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([0x7B, 0x7B, 0x7F], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Code(
                ([], [0x0B]),
                ([], [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x1A, 0x42, 0, 0x0B])
            )
        );
        var module = WasmModule.Decode(binary);
        var definition = module.Functions[1];

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(
                            WasmProcessingStage.Validate,
                            definition.Instructions[^1].ByteOffset,
                            2,
                            10
                        )
                    );
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
                await Assert.That(module.Functions[1]).IsSameReferenceAs(definition);
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }

    [Test]
    public async Task Typed_selectの両候補がUnknown_V128注釈の到達不能命令を受理する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0, 0x1C, 1, 0x7B, 0x1A, 0x0B]))
            )
        );

        // Act
        var validated = module.Validate();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(false, 0x7B, 0x7F, 0x7F)]
    [Arguments(true, 0x7B, 0x7F, 0x7F)]
    [Arguments(false, 0x7F, 0x7B, 0x7F)]
    [Arguments(true, 0x7F, 0x7B, 0x7F)]
    [Arguments(false, 0x7B, 0x7B, 0x7E)]
    [Arguments(true, 0x7B, 0x7B, 0x7E)]
    public async Task V128のselect候補か条件の型が不一致_Select位置で拒否し再検証でも成功状態を公開しない(
        bool typed,
        int firstType,
        int secondType,
        int conditionType
    )
    {
        // Arrange
        byte[] select = typed ? [0x1C, 1, 0x7B] : [0x1B];
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(
                    ([(byte)firstType, (byte)secondType, (byte)conditionType], [0x7B])
                ),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x0B]))
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(
                            WasmProcessingStage.Validate,
                            module.Functions[0].Instructions[3].ByteOffset,
                            0,
                            10
                        )
                    );
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}
