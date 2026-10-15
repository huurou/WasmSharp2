using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false, "FC0801000B", "unknown memory 0", 5)]
    [Arguments(false, "00FC0801000B", "unknown memory 0", 5)]
    [Arguments(true, "FC0801000B", "unknown data segment 1", 5)]
    [Arguments(true, "00FC0801000B", "unknown data segment 1", 5)]
    [Arguments(false, "FC09010B", "unknown data segment 1", 4)]
    [Arguments(false, "00FC09010B", "unknown data segment 1", 4)]
    [Arguments(true, "FC08FFFFFFFF0F000B", "unknown data segment 4294967295", 9)]
    [Arguments(false, "00FC09FFFFFFFF0F0B", "unknown data segment 4294967295", 8)]
    [Arguments(true, "FC0800000B", "type mismatch", 5)]
    [Arguments(true, "410041004200FC0800000B", "type mismatch", 5)]
    [Arguments(true, "410042004100FC0800000B", "type mismatch", 5)]
    [Arguments(true, "420041004100FC0800000B", "type mismatch", 5)]
    [Arguments(true, "004200FC0800000B", "type mismatch", 5)]
    [Arguments(false, "4100FC09000B", "type mismatch", 1)]
    public async Task データ参照命令に参照や型の違反がある_到達性によらずメモリとデータと型の順でValidateが拒否する(
        bool hasMemory,
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var data = HostLinkingModuleBinary.Section(11, [1, 1, 0]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions))),
            data
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
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        bytes.Length - data.Length - offsetFromEnd,
                        0,
                        10
                    )
                );
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments(true, "00FC0800000B")]
    [Arguments(false, "00FC09000B")]
    [Arguments(true, "00FC88008000000B")]
    [Arguments(false, "FC890080000B")]
    public async Task データ参照が有効で具体型の違反がない_非最短LEBと到達不能スタックを受理する(
        bool hasMemory,
        string instructions
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions))),
            HostLinkingModuleBinary.Section(11, [1, 1, 0])
        );
        var module = WasmModule.Decode(bytes);

        // Act
        var validated = module.Validate();
        var instance = validated.Instantiate([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(instance).IsNotNull();
        }
    }

    [Test]
    public async Task データ数が0でデータセクションがなくコードがデータを参照する_Decodeは成功してValidateで未知のデータを拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Section(12, [0]),
                HostLinkingModuleBinary.Code(([], [0xFC, 9, 0, 0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Message.StartsWith("unknown data segment 0", StringComparison.Ordinal))
            .IsTrue();
    }
}
