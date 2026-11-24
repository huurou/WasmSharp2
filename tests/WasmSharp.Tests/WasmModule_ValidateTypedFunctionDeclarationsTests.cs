using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    public async Task 引数と結果に複数の未解決参照を持つ関数型を読む_検証で最初の参照先とその位置を報告する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, [1, 0x60, 2, 0x63, 7, 0x63, 8, 1, 0x63, 9])),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown type 7");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 14, null, 1));
        }
    }
}
