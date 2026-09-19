using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Importのstartが引数結果0個_提供元なしで検証できcallbackを実行しない(
        bool useStream
    )
    {
        // Arrange
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "g", 3, [0x7F, 0]),
                ("env", "other", 0, [0]),
                ("env", "t", 1, [0x70, 0, 0]),
                ("env", "start", 0, [1])
            ),
            HostLinkingModuleBinary.Functions(2),
            HostLinkingModuleBinary.Globals((0x7F, false, [0x23, 0, 0x0B])),
            HostLinkingModuleBinary.Exports(("run", 0, 2)),
            HostLinkingModuleBinary.Start(1),
            HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);

        // Act
        var validated = module.Validate();
        var repeated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
        await Assert.That(repeated).IsSameReferenceAs(module);
        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(module.FunctionExportIndices["run"]).IsEqualTo(2);
        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    [Arguments("7F", "")]
    [Arguments("", "7F")]
    [Arguments("", "7F7E")]
    [Arguments("7F", "7F")]
    public async Task Importのstartが引数または結果を持つ_start位置で拒否する(
        string parameters,
        string results
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(
                    (Convert.FromHexString(parameters), Convert.FromHexString(results))
                ),
                HostLinkingModuleBinary.Imports(("", "", 0, [0])),
                HostLinkingModuleBinary.Start(0)
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, module.Start!.Value.ByteOffset, 0, 8));
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, 0u)]
    [Arguments(true, 2u)]
    [Arguments(true, uint.MaxValue)]
    public async Task Startが関数添字空間の範囲外_再検証しても成果を反映しない(
        bool hasFunctions,
        uint index
    )
    {
        // Arrange
        var sections = hasFunctions
            ? new[]
            {
                HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                HostLinkingModuleBinary.Imports(("", "", 0, [0])),
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Exports(("run", 0, 1)),
                HostLinkingModuleBinary.Start(index),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B])),
            }
            : new[] { HostLinkingModuleBinary.Start(index) };
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(sections));

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, module.Start!.Value.ByteOffset, index, 8)
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 定義startに結果がある_import数を引いて型を解決し拒否する(bool hasImport)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                hasImport
                    ? HostLinkingModuleBinary.Imports(("", "", 0, [0]))
                    : HostLinkingModuleBinary.Imports(),
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Start(hasImport ? 1u : 0u),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x0B]))
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
                    module.Start!.Value.ByteOffset,
                    hasImport ? 1u : 0u,
                    8
                )
            );
    }

    [Test]
    public async Task 定義startの型は有効だが実行形が未対応_検証未完了として拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(("", "", 0, [0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Start(1),
                HostLinkingModuleBinary.Code(([], [0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo("function.results");
        await Assert
            .That(exception.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, module.Functions[0].BodyOffset, 1, 10));
        await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(1);
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }
}
