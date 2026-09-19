using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    public async Task 各種類のimportと定義をexportする_種類別の添字空間を受理する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(
                    ("env", "g", 3, [0x7F, 0]),
                    ("env", "t", 1, [0x70, 0, 0]),
                    ("env", "f", 0, [0]),
                    ("env", "m", 2, [0, 0])
                ),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x6F, 0, uint.MaxValue)),
                HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 0, 0x0B])),
                HostLinkingModuleBinary.Exports(
                    ("f0", 0, 0),
                    ("f1", 0, 1),
                    ("t0", 1, 0),
                    ("t1", 1, 1),
                    ("m0", 2, 0),
                    ("g0", 3, 0),
                    ("g1", 3, 1)
                ),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]))
            )
        );

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes.Length).IsEqualTo(1);
        await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments((byte)0, 1u)]
    [Arguments((byte)1, 1u)]
    [Arguments((byte)2, 1u)]
    [Arguments((byte)3, 1u)]
    [Arguments((byte)0, uint.MaxValue)]
    [Arguments((byte)1, uint.MaxValue)]
    [Arguments((byte)2, uint.MaxValue)]
    [Arguments((byte)3, uint.MaxValue)]
    public async Task Export添字が種類別の範囲外_宣言位置付きで拒否する(byte kind, uint index)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(
                    ("", "", 3, [0x7F, 0]),
                    ("", "", 0, [0]),
                    ("", "", 1, [0x70, 0, 0]),
                    ("", "", 2, [0, 0])
                ),
                HostLinkingModuleBinary.Exports(("", kind, index))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Exports[0].ByteOffset,
                    kind == 0 ? index : null,
                    7
                )
            );
    }

    [Test]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    public async Task Importの関数型添字が範囲外_関数添字とimport位置付きで拒否する(uint typeIndex)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(
                    ("env", "g", 3, [0x7F, 0]),
                    ("env", "f", 0, [0]),
                    ("env", "f", 0, HostLinkingModuleBinary.Unsigned(typeIndex))
                )
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, module.Imports[2].ByteOffset, 1, 2));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Import後の定義関数が不正_型参照と本体の診断がmodule全体の関数添字になる(
        bool invalidType
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(
                    ("", "", 0, [0]),
                    ("", "", 3, [0x7F, 0]),
                    ("", "", 0, [0])
                ),
                HostLinkingModuleBinary.Functions(invalidType ? 1u : 0u),
                HostLinkingModuleBinary.Code(([], [0x42, 0, 0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    invalidType
                        ? module.Functions[0].BodyOffset
                        : module.Functions[0].Instructions[^1].ByteOffset,
                    2,
                    10
                )
            );
    }

    [Test]
    public async Task 種類を跨いでexport名が重複_二つ目の宣言位置で拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Memories((0, null)),
                HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 0, 0x0B])),
                HostLinkingModuleBinary.Exports(("same", 2, 0), ("same", 3, 0))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, module.Exports[1].ByteOffset, null, 7));
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Memoryのimportと定義の合計が2個_二つ目の宣言で拒否する(int importCount)
    {
        // Arrange
        var imports = Enumerable
            .Range(0, importCount)
            .Select(x => ("", "", (byte)2, new byte[] { 0, 0 }))
            .ToArray();
        var memories = Enumerable
            .Range(0, 2 - importCount)
            .Select(x => (0u, (uint?)null))
            .ToArray();
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Imports(imports),
                HostLinkingModuleBinary.Memories(memories)
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    importCount == 2
                        ? module.Imports[1].ByteOffset
                        : module.Memories[^1].ByteOffset,
                    null,
                    importCount == 2 ? (byte)2 : (byte)5
                )
            );
    }

    [Test]
    [Arguments((byte)2, 2u, 1u, false)]
    [Arguments((byte)2, 2u, 1u, true)]
    [Arguments((byte)2, 65537u, null, false)]
    [Arguments((byte)2, 65537u, null, true)]
    [Arguments((byte)2, 0u, 65537u, false)]
    [Arguments((byte)2, 0u, 65537u, true)]
    [Arguments((byte)1, 2u, 1u, false)]
    [Arguments((byte)1, 2u, 1u, true)]
    public async Task Limitsが仕様に不適合_割当前にValidateで拒否する(
        byte kind,
        uint minimum,
        uint? maximum,
        bool imported
    )
    {
        // Arrange
        var limits = HostLinkingModuleBinary.Limits(minimum, maximum);
        var section =
            imported
                ? HostLinkingModuleBinary.Imports(
                    ("", "", kind, kind == 1 ? [0x70, .. limits] : limits)
                )
            : kind == 1 ? HostLinkingModuleBinary.Tables((0x70, minimum, maximum))
            : HostLinkingModuleBinary.Memories((minimum, maximum));
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(section));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
        await Assert
            .That(exception.Location.SectionId)
            .IsEqualTo(
                imported ? (byte)2
                : kind == 1 ? (byte)4
                : (byte)5
            );
        await Assert.That(exception.Location.FunctionIndex).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Memoryと複数tableのlimitsが仕様上限_実割当なしで受理する(bool imported)
    {
        // Arrange
        var sections = imported
            ? new[]
            {
                HostLinkingModuleBinary.Imports(
                    ("", "", 2, HostLinkingModuleBinary.Limits(65536, 65536)),
                    (
                        "",
                        "",
                        1,
                        [0x70, .. HostLinkingModuleBinary.Limits(uint.MaxValue, uint.MaxValue)]
                    ),
                    ("", "", 1, [0x6F, .. HostLinkingModuleBinary.Limits(uint.MaxValue)])
                ),
            }
            : new[]
            {
                HostLinkingModuleBinary.Tables(
                    (0x70, uint.MaxValue, uint.MaxValue),
                    (0x6F, uint.MaxValue, null)
                ),
                HostLinkingModuleBinary.Memories((65536, 65536)),
            };
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(sections));

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes.IsEmpty).IsTrue();
    }
}
