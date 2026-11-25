using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments((byte)2)] // 次のグループ
    [Arguments((byte)3)] // モジュールの範囲外
    public async Task グループの最後を越えた型番号を使う_Decodeが成功しValidateが参照先の位置を報告する(byte index)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x4E, 2, 0x60, 1, 0x64, 1, 0, 0x60, 0, 1, 0x63, index, 0x60, 0, 0])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith($"unknown type {index}");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 22, null, 1));
        }
    }

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
