using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 外部要素の検証に成功_関数exportだけを全体添字で確定し再検証できる(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(("", "", 0, [0]), ("", "", 3, [0x7B, 0])),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Tables((0x70, uint.MaxValue, null), (0x6F, 0, 1)),
            HostLinkingModuleBinary.Memories((65536, 65536)),
            HostLinkingModuleBinary.Exports(
                ("", 0, 0),
                ("run", 0, 1),
                ("RUN", 1, 0),
                ("é", 1, 1),
                ("e\u0301", 2, 0),
                ("global", 3, 0)
            ),
            HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);

        // Act
        var validated = module.Validate();
        var repeated = module.Validate();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(repeated).IsSameReferenceAs(module);
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
            await Assert.That(module.FunctionExportIndices.Count).IsEqualTo(2);
            await Assert.That(module.FunctionExportIndices[""]).IsEqualTo(0);
            await Assert.That(module.FunctionExportIndices["run"]).IsEqualTo(1);
        }
        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([]))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo("section.import");
        await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
        await Assert.That(exception.UnverifiedRanges.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments((byte)4, "01700000", "section.table")]
    [Arguments((byte)5, "010000", "section.memory")]
    public async Task 資源定義の検証に成功_構築の未対応はInstantiateで拒否する(
        byte id,
        string hex,
        string feature
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(id, Convert.FromHexString(hex))
            )
        );

        // Act
        module.Validate();

        // Assert
        await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([]))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo(feature);
        await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
    }

    [Test]
    public async Task Import後の後半関数が検証失敗_再試行してもコードとexport索引を反映しない()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(("", "", 0, [0])),
                HostLinkingModuleBinary.Functions(0, 0),
                HostLinkingModuleBinary.Exports(("imported", 0, 0), ("defined", 0, 1)),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]), ([], [0x42, 0, 0x0B]))
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert.That(exception!.Location!.FunctionIndex).IsEqualTo(2u);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
