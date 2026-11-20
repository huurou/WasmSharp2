using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments((byte)0, "constant expression required")]
    [Arguments((byte)1, "type mismatch")]
    public async Task Core3のデータオフセットで可変globalまたはi64を参照する_検証で拒否する(byte index, string diagnostic)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B]), (0x7E, false, [0x42, 0, 0x0B])),
            HostLinkingModuleBinary.Section(11, [1, 0, 0x23, index, 0x0B, 0])
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith(diagnostic);
    }

    [Test]
    [Arguments(false, "", "", "010041000B00", "unknown memory 0", 5, "データセグメント")]
    [Arguments(true, "", "", "01020141000B00", "unknown memory 1", 6, "データセグメント")]
    [Arguments(true, "", "", "010042000B00", "type mismatch", 2, null)]
    [Arguments(true, "", "", "01000B00", "type mismatch", 2, null)]
    [Arguments(true, "", "", "0100410041006A0B00", "constant expression required", 3, null)]
    [Arguments(true, "020A0103656E760167037F01", "", "010023000B00", "constant expression required", 4, null)]
    [Arguments(true, "", "0606017F0041000B", "010023000B00", "unknown global 0", 4, null)]
    public async Task データのメモリ参照や定数式が不正である_Decodeは成功してValidateで拒否する(
        bool hasMemory,
        string imports,
        string globals,
        string data,
        string diagnostic,
        int offsetFromEnd,
        string? expectedDetail
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            Convert.FromHexString(imports),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            Convert.FromHexString(globals),
            HostLinkingModuleBinary.Section(11, Convert.FromHexString(data))
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            if (expectedDetail is not null)
            {
                await Assert.That(exception.Message).Contains(expectedDetail);
            }
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - offsetFromEnd, null, 11));
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }
}
