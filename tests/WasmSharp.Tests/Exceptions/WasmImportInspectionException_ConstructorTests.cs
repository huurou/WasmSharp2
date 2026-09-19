using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

internal class WasmImportInspectionException_ConstructorTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 未確認範囲にdefaultまたは空配列を指定する_空配列へ正規化して診断を保持する(
        bool useDefault
    )
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Decode, 37, null, 2);
        ImmutableArray<WasmUnverifiedRange> ranges = useDefault ? default : [];
        var innerException = new InvalidOperationException("原因");

        // Act
        var exception = new WasmImportInspectionException(
            "import情報を取得できない",
            WasmImportInspectionReason.UnsupportedFeature,
            "import.type",
            location,
            ranges,
            innerException
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.UnverifiedRanges.IsDefault).IsFalse();
            await Assert.That(exception.UnverifiedRanges.ToArray()).IsEmpty();
            await Assert
                .That(exception.Reason)
                .IsEqualTo(WasmImportInspectionReason.UnsupportedFeature);
            await Assert.That(exception.Feature).IsEqualTo("import.type");
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Message).IsEqualTo("import情報を取得できない");
            await Assert.That(exception.InnerException).IsSameReferenceAs(innerException);
        }
    }
}
