using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("D2001A0B", false, false, "undeclared function reference", 0)]
    [Arguments("00D2001A0B", false, false, "undeclared function reference", 1)]
    [Arguments("D2001A0B", true, false, "undeclared function reference", 0)]
    [Arguments("D2001A0B", false, true, "undeclared function reference", 0)]
    [Arguments("D2011A0B", false, false, "unknown function 1", 0)]
    [Arguments("00D2FFFFFFFF0F1A0B", false, false, "unknown function 4294967295", 1)]
    public async Task 関数本体とstartとimportだけが関数を参照する_宣言にならず未知添字を先に拒否する(
        string body,
        bool start,
        bool import,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        byte[][] declarations = import
            ? [HostLinkingModuleBinary.Imports(("env", "f", 0, [0]))]
            : [];
        byte[][] sections =
        [
            HostLinkingModuleBinary.Types(([], [])),
            .. declarations,
            HostLinkingModuleBinary.Functions(0),
            .. (start ? new[] { HostLinkingModuleBinary.Start(0) } : []),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(body))),
        ];
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(sections));

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[instructionIndex].ByteOffset,
                        import ? 1u : 0u,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("4100D10B", "type mismatch", 1)]
    [Arguments("4200D10B", "type mismatch", 1)]
    [Arguments("D070D07041001B1A0B", "type mismatch", 3)]
    [Arguments("D070D07041001C001A0B", "invalid result arity", 3)]
    [Arguments("00001C02706F1A0B", "invalid result arity", 2)]
    [Arguments("D070D06F41001C01701A0B", "type mismatch", 3)]
    [Arguments("D070D07042001C01701A0B", "type mismatch", 3)]
    [Arguments("D07041001C01701A0B", "type mismatch", 2)]
    [Arguments("00D07041001B1A0B", "type mismatch", 3)]
    public async Task 参照命令とselectの型や注釈が不正_元命令位置のValidate失敗として拒否する(
        string body,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[instructionIndex].ByteOffset,
                        0,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments("00D10B", (byte)0x7F)]
    [Arguments("001C01700B", (byte)0x70)]
    [Arguments("001C016F0B", (byte)0x6F)]
    public async Task 到達不能の参照operandがUnknown_参照判定とtyped_selectを検証できる(
        string body,
        byte result
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [result])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments("D201D10B", false, "constant expression required", 1)]
    [Arguments("D2011A0B", false, "constant expression required", 1)]
    [Arguments("D20123010B", false, "unknown global 1", 1)]
    [Arguments("D20123000B", true, "constant expression required", 1)]
    [Arguments("D2010B", false, "unknown function 1", 0)]
    [Arguments("D070D06F0B", false, "type mismatch", 2)]
    [Arguments("D06F0B", false, "type mismatch", 1)]
    [Arguments("0B", false, "type mismatch", 0)]
    public async Task 参照定数式に複合不正がある_定数性とglobal適格性と参照添字と結果の順に検証する(
        string initializer,
        bool mutable,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(
                    ("env", "g", 3, [0x70, mutable ? (byte)1 : (byte)0])
                ),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Globals((0x70, false, Convert.FromHexString(initializer))),
                HostLinkingModuleBinary.Code(([], [0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Globals[0].Initializer[instructionIndex].ByteOffset,
                        null,
                        6
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 定義参照globalを定数式から取得する_Validateでunknown_globalを返す()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Globals(
                    (0x70, false, [0xD0, 0x70, 0x0B]),
                    (0x70, false, [0x23, 0, 0x0B])
                )
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Message.StartsWith("unknown global 0", StringComparison.Ordinal))
            .IsTrue();
    }
}
