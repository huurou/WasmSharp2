using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryReader_UnsupportedTests
{
    [Test]
    public async Task 未対応の診断を作る_機能と位置と未確認範囲を保持する()
    {
        // Arrange
        var reader = new ModuleBinaryReader([0x00, 0x00], 40, 10, 3);

        // Act
        var exception = reader.Unsupported("unsupported.instruction", 100, 41);
        var position = reader.Position;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Feature).IsEqualTo("unsupported.instruction");
            await Assert.That(exception.Message).IsEqualTo("未実装の機能に遭遇しました。");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 41, 3, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Decode,
                        41,
                        100,
                        "この構文以降のデコードが未完了です。"
                    )
                );
            await Assert
                .That(exception.UnverifiedRanges[1])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Validate,
                        0,
                        100,
                        "入力全体の検証が未実施です。"
                    )
                );
            await Assert.That(position).IsEqualTo(40L);
        }
    }
}
