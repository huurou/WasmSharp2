using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false, "2803000B", "unknown memory 0", 4)]
    [Arguments(false, "002803000B", "unknown memory 0", 4)]
    [Arguments(false, "003603000B", "unknown memory 0", 4)]
    [Arguments(true, "2803000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "002803000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "003603000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "2821000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "28FFFFFFFF0F000B", "alignment must not be larger than natural", 8)]
    [Arguments(true, "2802000B", "type mismatch", 4)]
    [Arguments(true, "42002802000B", "type mismatch", 4)]
    [Arguments(true, "41002802000B", "type mismatch", 1)]
    [Arguments(true, "410041003702000B", "type mismatch", 4)]
    [Arguments(true, "420042003702000B", "type mismatch", 4)]
    [Arguments(true, "0041003702000B", "type mismatch", 4)]
    public async Task メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する(
        bool hasMemory,
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - offsetFromEnd, 0, 10));
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("002802001A0B")]
    [Arguments("003703000B")]
    public async Task 到達不能なメモリ命令に具体型と参照とalignの違反がない_スタック多相性でValidateに成功する(
        string instructions
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        var module = WasmModule.Decode(bytes);

        // Act
        var validated = module.Validate();
        var instance = validated.Instantiate([]);

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
        await Assert.That(instance).IsNotNull();
    }

    [Test]
    public async Task 後半関数のメモリ命令が不正_検証済みの命令を部分公開せず再試行でも拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0, 0),
                HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Code(
                    ([], [0x41, 0, 0x28, 2, 0, 0x1A, 0x0B]),
                    ([], [0x28, 3, 0, 0x0B])
                )
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert.That(exception!.Location!.FunctionIndex).IsEqualTo(1u);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
