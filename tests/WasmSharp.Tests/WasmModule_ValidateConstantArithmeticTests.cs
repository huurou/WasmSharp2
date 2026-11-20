using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("41016A", 2)] // i32.addの左オペランドがない
    [Arguments("420141016B", 2)] // i32.subの左にi64
    [Arguments("410142016C", 2)] // i32.mulの右にi64
    [Arguments("420142027C", 1)] // 結果がi64
    [Arguments("4101410241036C", 1)] // 乗算後に2個の値が残る
    public async Task Core3の整数定数式でオペランドや結果が不正_型不一致が生じた位置を報告する(
        string instructions,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((0x7F, false, [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, bytes.Length - offsetFromEnd, null, 6));
        }
    }
}
