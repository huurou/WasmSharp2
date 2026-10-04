using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(true, true, true, "unknown local 0", (byte)10)]
    [Arguments(false, true, true, "unknown function 1", (byte)8)]
    [Arguments(false, false, true, "unknown function 1", (byte)7)]
    [Arguments(false, false, false, "multiple memories", (byte)5)]
    public async Task 関数とstartとexportとmemory数が不正_設計順で選び再検証でも成果を反映しない(
        bool invalidFunction,
        bool invalidStart,
        bool invalidExport,
        string prefix,
        byte sectionId
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Memories((0, null), (0, null)),
                HostLinkingModuleBinary.Exports(("run", 0, invalidExport ? 1u : 0u)),
                HostLinkingModuleBinary.Start(invalidStart ? 1u : 0u),
                HostLinkingModuleBinary.Code(([], invalidFunction ? [0x20, 0, 0x0B] : [0x0B]))
            )
        );
        var offset = sectionId switch
        {
            10 => module.Functions[0].Instructions[0].ByteOffset,
            8 => module.Start!.Value.ByteOffset,
            7 => module.Exports[0].ByteOffset,
            _ => module.Memories[1].ByteOffset,
        };
        var functionIndex = sectionId switch
        {
            10 => (uint?)0,
            8 or 7 => 1u,
            _ => null,
        };

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
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
                    .IsEqualTo(new(WasmProcessingStage.Validate, offset, functionIndex, sectionId));
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }

    [Test]
    [Arguments(false, "duplicate export name")]
    [Arguments(true, "unknown function 1")]
    public async Task Export名が重複し添字も不正_export内の添字を先に選ぶ(
        bool invalidIndex,
        string prefix
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(
                    ("same", 0, 0),
                    ("same", 0, invalidIndex ? 1u : 0u)
                ),
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
                        module.Exports[1].ByteOffset,
                        invalidIndex ? 1u : 0u,
                        7
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
    [Arguments(false)]
    [Arguments(true)]
    public async Task Importのstartが引数結果0個_提供元なしで検証しInstantiateまでcallbackを実行しない(
        bool useStream
    )
    {
        // Arrange
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define("g", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        host.Define(
            "other",
            WasmFunction.CreateHost(new([WasmValueKind.I32], [WasmValueKind.I32]), _ => new([]))
        );
        host.Define("t", new WasmTable(WasmValueKind.FuncRef, new(0)));
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
        var callsAfterValidation = calls;
        module.Instantiate([host]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(repeated).IsSameReferenceAs(module);
            await Assert.That(callsAfterValidation).IsEqualTo(0);
            await Assert.That(module.FunctionExportIndices["run"]).IsEqualTo(2);
            await Assert.That(calls).IsEqualTo(1);
        }
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
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("start function", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, module.Start!.Value.ByteOffset, 0, 8));
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
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
            : [HostLinkingModuleBinary.Start(index)];
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(sections));

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(
                        exception!.Message.StartsWith(
                            $"unknown function {index}",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
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
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("start function", StringComparison.Ordinal))
                .IsTrue();
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
    }

    [Test]
    public async Task 引数と結果が0個の定義startがある_実行せず検証に成功する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(("", "", 0, [0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Start(1),
                HostLinkingModuleBinary.Code(([], [0x00, 0x0B]))
            )
        );

        // Act
        var result = module.Validate();

        // Assert
        await Assert.That(result).IsSameReferenceAs(module);
        await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
    }
}
