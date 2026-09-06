using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

public class WasmUnsupportedFeatureException_ConstructorTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 未確認範囲にdefaultまたは空配列を指定する_列挙可能な空配列で取得できる(
        bool useDefault
    )
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Validate);
        ImmutableArray<WasmUnverifiedRange> ranges = useDefault ? default : [];

        // Act
        var exception = new WasmUnsupportedFeatureException(
            "診断範囲なし",
            "function.parameters",
            location,
            ranges
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.UnverifiedRanges.IsDefault).IsFalse();
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(0);
            await Assert.That(exception.UnverifiedRanges.ToArray()).IsEmpty();
            await Assert.That(exception.Feature).IsEqualTo("function.parameters");
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    [Test]
    public async Task 未実装の診断を指定する_機能名と位置と段階別の未確認範囲を保持する()
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Decode, 37, 2, 10);
        ImmutableArray<WasmUnverifiedRange> ranges =
        [
            new(WasmProcessingStage.Decode, 37, 100, "未対応命令以降の構文"),
            new(WasmProcessingStage.Validate, 0, 100, "入力全体の検証"),
        ];
        var innerException = new InvalidOperationException("原因");

        // Act
        var exception = new WasmUnsupportedFeatureException(
            "処理を中断した",
            "i32.add",
            location,
            ranges,
            innerException
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Feature).IsEqualTo("i32.add");
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Message).IsEqualTo("処理を中断した");
            await Assert.That(ReferenceEquals(exception.InnerException, innerException)).IsTrue();
            await Assert.That(exception.UnverifiedRanges.IsDefault).IsFalse();
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Decode,
                        37,
                        100,
                        "未対応命令以降の構文"
                    )
                );
            await Assert
                .That(exception.UnverifiedRanges[1])
                .IsEqualTo(
                    new WasmUnverifiedRange(WasmProcessingStage.Validate, 0, 100, "入力全体の検証")
                );
        }
    }

    [Test]
    public async Task 既存の構築経路を使う_未確認範囲を列挙可能な空配列で取得できる()
    {
        // Arrange
        var innerException = new InvalidOperationException("原因");

        // Act
        WasmUnsupportedFeatureException[] exceptions =
        [
            new(),
            new("未実装"),
            new("未実装", innerException),
            new(null),
            new(null, null),
        ];

        // Assert
        using (Assert.Multiple())
        {
            foreach (var exception in exceptions)
            {
                await Assert.That(exception.UnverifiedRanges.IsDefault).IsFalse();
                await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(0);
                await Assert.That(exception.UnverifiedRanges.ToArray()).IsEmpty();
                await Assert.That(exception.Location).IsNull();
                await Assert.That(exception.Feature).IsNull();
            }
            await Assert.That(exceptions[1].Message).IsEqualTo("未実装");
            await Assert.That(exceptions[2].Message).IsEqualTo("未実装");
            await Assert
                .That(ReferenceEquals(exceptions[2].InnerException, innerException))
                .IsTrue();
        }
    }
}
