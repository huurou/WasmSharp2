using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments("FD1510")]
    [Arguments("FD1808")]
    [Arguments("FD1B04")]
    [Arguments("FD1D02")]
    [Arguments("FD0D00000000000000000000000000000020")]
    [Arguments("FD0D20000000000000000000000000000000")]
    public async Task SIMD接続のlaneかshuffleが上限にある_到達不能でもValidateで位置付き拒否し成功状態を確定しない(
        string instruction
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(
                ([], [0, .. Convert.FromHexString(instruction), 0x1A, 0x0B])
            )
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        var offset = Array.IndexOf(bytes, (byte)0xFD);

        foreach (var module in modules)
        {
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
                                "invalid lane index",
                                StringComparison.Ordinal
                            )
                        )
                        .IsTrue();
                    await Assert
                        .That(exception!.Location)
                        .IsEqualTo(new(WasmProcessingStage.Validate, offset, 0, 10));
                    await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                    await Assert
                        .That(() => module.Instantiate([]))
                        .ThrowsExactly<InvalidOperationException>();
                }
            }
        }
    }

    [Test]
    [Arguments("7F7B", "FD1700")]
    [Arguments("7E7B", "FD1E00")]
    [Arguments("7D7B", "FD2000")]
    [Arguments("7C7B", "FD2200")]
    [Arguments("7B", "FD52")]
    [Arguments("7F7B", "FD4E")]
    public async Task SIMD接続の入力型か個数が不適合_Validateで命令位置の型不一致を返す(
        string parameters,
        string instruction
    )
    {
        // Arrange
        var types = Convert.FromHexString(parameters);
        var pushes = types.SelectMany((_, x) => new byte[] { 0x20, (byte)x });
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((types, [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(
                ([], [.. pushes, .. Convert.FromHexString(instruction), 0x0B])
            )
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, Array.IndexOf(bytes, (byte)0xFD), 0, 10)
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments("FD150F")]
    [Arguments("FD1807")]
    [Arguments("FD1B03")]
    [Arguments("FD1D01")]
    [Arguments("FD0D1F1F1F1F1F1F1F1F1F1F1F1F1F1F1F1F")]
    [Arguments("FD2201")]
    public async Task SIMD接続の到達不能命令が静的制約を満たす_不足operandには既存の多相型規則を適用する(
        string instruction
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    ([], [0, .. Convert.FromHexString(instruction), 0x1A, 0x0B])
                )
            )
        );

        // Act
        module.Validate();

        // Assert
        await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
    }

    [Test]
    [Arguments("FD4D")]
    [Arguments("FD1500")]
    [Arguments("FD0D00000000000000000000000000000000")]
    public async Task SIMD接続の演算がglobal初期化式にある_Decodeは保持しValidateで定数式として拒否する(
        string instruction
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals(
                (
                    0x7B,
                    false,
                    [0xFD, 0x0C, .. new byte[16], .. Convert.FromHexString(instruction), 0x0B]
                )
            )
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "constant expression required",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception!.Location!.SectionId).IsEqualTo((byte?)6);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
