using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false, "", "", "010041000B00", "unknown memory 0", 5)]
    [Arguments(true, "", "", "01020141000B00", "unknown memory 1", 6)]
    [Arguments(true, "", "", "010042000B00", "type mismatch", 2)]
    [Arguments(true, "", "", "01000B00", "type mismatch", 2)]
    [Arguments(true, "", "", "0100410041006A0B00", "constant expression required", 3)]
    [Arguments(
        true,
        "020A0103656E760167037F01",
        "",
        "010023000B00",
        "constant expression required",
        4
    )]
    [Arguments(true, "", "0606017F0041000B", "010023000B00", "unknown global 0", 4)]
    public async Task データのメモリ参照や定数式が不正である_Decodeは成功してValidateで拒否する(
        bool hasMemory,
        string imports,
        string globals,
        string data,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            Convert.FromHexString(imports),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            Convert.FromHexString(globals),
            HostLinkingModuleBinary.Section(11, Convert.FromHexString(data))
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
                    new(WasmProcessingStage.Validate, bytes.Length - offsetFromEnd, null, 11)
                );
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
