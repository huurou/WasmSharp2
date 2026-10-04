using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task 名前不足と不適合importがある_宣言順によらず名前不足を選ぶ(
        bool missingFirst,
        bool kindMismatch
    )
    {
        // Arrange
        (string, string, byte, byte[]) missing = ("missing", "g", 3, [0x7F, 0]);
        (string, string, byte, byte[]) incompatible = ("env", "f", 0, [0]);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        missingFirst ? missing : incompatible,
                        missingFirst ? incompatible : missing
                    )
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        if (kindMismatch)
        {
            host.Define("f", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        }
        else
        {
            host.Define("f", WasmFunction.CreateHost(new([WasmValueKind.I32], []), _ => new([])));
        }
        var ordinal = missingFirst ? 0 : 1;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.MissingImport);
            await Assert
                .That(exception.Message.StartsWith("unknown import", StringComparison.Ordinal))
                .IsTrue();
            await Assert.That(exception.ImportOrdinal).IsEqualTo(ordinal);
            await Assert.That(exception.ModuleName).IsEqualTo("missing");
            await Assert.That(exception.ImportName).IsEqualTo("g");
            await Assert.That(exception.ExpectedKind).IsEqualTo(WasmExternalKind.Global);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Instantiate,
                        module.Imports[ordinal].ByteOffset,
                        null,
                        2
                    )
                );
        }
    }

    [Test]
    public async Task 複数の名前不足がある_最初の宣言を選ぶ()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(
                        ("first", "g", 3, [0x7F, 0]),
                        ("last", "t", 1, [0x70, 0, 0])
                    )
                )
            )
            .Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(new WasmImports()))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.MissingImport);
            await Assert
                .That(exception.Message.StartsWith("unknown import", StringComparison.Ordinal))
                .IsTrue();
            await Assert.That(exception.ImportOrdinal).IsEqualTo(0);
            await Assert.That(exception.ModuleName).IsEqualTo("first");
            await Assert.That(exception.ImportName).IsEqualTo("g");
            await Assert.That(exception.ExpectedKind).IsEqualTo(WasmExternalKind.Global);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Instantiate, module.Imports[0].ByteOffset, null, 2)
                );
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task 複数importの種類または型が不適合_最後の宣言を選ぶ(
        bool firstKindMismatch,
        bool lastKindMismatch
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("first", "f", 0, [0]),
                        ("last", "g", 3, [0x7F, 0])
                    )
                )
            )
            .Validate();
        var first = new WasmHostModule("first");
        if (firstKindMismatch)
        {
            first.Define("f", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        }
        else
        {
            first.Define("f", WasmFunction.CreateHost(new([WasmValueKind.I32], []), _ => new([])));
        }
        var last = new WasmHostModule("last");
        if (lastKindMismatch)
        {
            last.Define("g", WasmFunction.CreateHost(new([], []), _ => new([])));
        }
        else
        {
            last.Define("g", new WasmGlobal(new(WasmValueKind.I64, false), WasmValue.FromI64(0)));
        }

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([first, last]))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(
                    lastKindMismatch
                        ? WasmInstantiateReason.KindMismatch
                        : WasmInstantiateReason.TypeMismatch
                );
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "incompatible import type",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert.That(exception.ImportOrdinal).IsEqualTo(1);
            await Assert.That(exception.ModuleName).IsEqualTo("last");
            await Assert.That(exception.ImportName).IsEqualTo("g");
            await Assert.That(exception.ExpectedKind).IsEqualTo(WasmExternalKind.Global);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Instantiate, module.Imports[1].ByteOffset, null, 2)
                );
        }
    }
}
