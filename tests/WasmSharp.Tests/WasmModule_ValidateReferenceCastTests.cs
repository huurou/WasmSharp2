using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("D070D06CD31A", 4, "type mismatch")] // ref.eqの左がfuncref
    [Arguments("D06CD06ED31A", 4, "type mismatch")] // ref.eqの右がanyref
    [Arguments("D06FFB146C1A", 2, "type mismatch")] // externrefをi31に照合する。
    [Arguments("4100FB17001A", 2, "type mismatch")] // i32を関数型にキャストする。
    [Arguments("00FB1801006C6B", 1, "type mismatch on cast: キャスト先はキャスト元の部分型")] // キャスト先のstructはi31の部分型にならない。
    [Arguments("00FB1901006E70", 1, "type mismatch on cast")] // anyとfuncは異なる階層に属する。
    [Arguments("00FB1802006E6C", 1, "type mismatch on cast")] // キャスト先だけがnullを許可する。
    [Arguments("00FB1801006E6C", 1, "type mismatch")] // ラベルが空
    [Arguments("02646C00FB1803006C6C000B1A", 4, "type mismatch")] // nullableな成功値を非nullのラベルへ渡す。
    [Arguments("02646C00FB1901006C6C000B1A", 4, "type mismatch: 分岐先の最後の型")] // nullableな失敗値を非nullのラベルへ渡す。
    [Arguments("02014207D06EFB1801006E6C000B1A1A", 6, "type mismatch")] // ラベルの先行するi32の位置にi64がある。
    [Arguments("02014107D06FFB1901006E6C000B1A1A", 6, "type mismatch")] // スタックのexternrefがキャスト元anyrefに合わない。
    [Arguments("00FB1901016E6C", 1, "unknown label 1")]
    [Arguments("00FB1402", 3, "unknown type 2")]
    [Arguments("00FB190100036C", 5, "unknown type 3")]
    [Arguments("00FB1901006E04", 6, "unknown type 4")]
    public async Task 参照の比較またはキャストの型が不正である_検証時に原因と位置を報告する(
        string instructions,
        int offset,
        string diagnostic
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(instructions);
        var module = ReferenceControlModule.Create([2, 0x60, 0, 0, 0x60, 0, 2, 0x7F, 0x6E], bytes);
        var start = module.InputLength - 1 - bytes.Length;

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, start + offset, 0, 10));
        }
    }
}
