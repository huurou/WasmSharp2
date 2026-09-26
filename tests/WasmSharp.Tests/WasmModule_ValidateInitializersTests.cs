using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments((byte)0x7F, "41000B")]
    [Arguments((byte)0x7E, "42000B")]
    [Arguments((byte)0x7D, "43000000000B")]
    [Arguments((byte)0x7C, "4400000000000000000B")]
    public async Task Globalのスカラー定数が宣言型と一致_評価せず同じmoduleを検証済みにする(
        byte kind,
        string initializer
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Globals(
                    (kind, false, Convert.FromHexString(initializer)),
                    (kind, true, Convert.FromHexString(initializer))
                ),
                HostLinkingModuleBinary.Exports(("g", 3, 1))
            )
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
        await Assert.That(module.Validate()).IsSameReferenceAs(module);
        await Assert
            .That(module.Instantiate([]).GetGlobal("g").Kind)
            .IsEqualTo(module.Globals[1].Type.ValueKind);
    }

    [Test]
    [Arguments("20000B")]
    [Arguments("10000B")]
    [Arguments("24000B")]
    [Arguments("000B")]
    public async Task Global初期化式で許可されない命令がある_デコード後に命令位置で検証を拒否する(
        string initializer
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Globals((0x7F, false, Convert.FromHexString(initializer)))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Globals[0].Initializer[0].ByteOffset,
                    null,
                    6
                )
            );
    }

    [Test]
    [Arguments("0B")]
    [Arguments("410041010B")]
    [Arguments("42000B")]
    public async Task Global式の結果の個数や型が不一致_終端位置で拒否して成果を反映しない(
        string initializer
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Globals(
                    (0x7F, false, [0x41, 0, 0x0B]),
                    (0x7F, false, Convert.FromHexString(initializer))
                ),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]))
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Globals[1].Initializer[^1].ByteOffset,
                        null,
                        6
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments((byte)0x7F)]
    [Arguments((byte)0x7E)]
    [Arguments((byte)0x7D)]
    [Arguments((byte)0x7C)]
    [Arguments((byte)0x7B)]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task ImportedImmutableGlobalから初期化_全7型をglobal添字で解決し受理する(byte kind)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(
                    ("", "", 0, [0]),
                    ("", "", 3, [0x7F, 0]),
                    ("", "", 1, [0x70, 0, 0]),
                    ("", "", 3, [kind, 0])
                ),
                HostLinkingModuleBinary.Globals(
                    (kind, false, [0x23, 1, 0x0B]),
                    (kind, true, [0x23, 1, 0x0B])
                ),
                HostLinkingModuleBinary.Exports(("g", 3, 3))
            )
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
        await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments(true, 0u)]
    [Arguments(false, 1u)]
    [Arguments(false, 2u)]
    [Arguments(false, 3u)]
    [Arguments(false, uint.MaxValue)]
    public async Task Global取得がmutableまたは定義または範囲外_命令位置で拒否する(
        bool mutableImport,
        uint index
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Imports(
                    ("", "", 3, [0x7F, mutableImport ? (byte)1 : (byte)0])
                ),
                HostLinkingModuleBinary.Globals(
                    (0x7F, false, [0x41, 0, 0x0B]),
                    (0x7F, false, [0x23, .. HostLinkingModuleBinary.Unsigned(index), 0x0B])
                )
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Globals[1].Initializer[0].ByteOffset,
                    null,
                    6
                )
            );
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments((byte)0x7E, (byte)0x7F)]
    [Arguments((byte)0x70, (byte)0x6F)]
    [Arguments((byte)0x7B, (byte)0x7F)]
    public async Task ImportedGlobal取得の型が宣言と不一致_暗黙変換せず拒否する(
        byte importedKind,
        byte declaredKind
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Imports(("", "", 3, [importedKind, 0])),
                HostLinkingModuleBinary.Globals((declaredKind, false, [0x23, 0, 0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Globals[0].Initializer[^1].ByteOffset,
                    null,
                    6
                )
            );
    }
}
