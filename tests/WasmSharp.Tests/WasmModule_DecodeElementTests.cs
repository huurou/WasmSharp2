using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("00", false)]
    [Arguments("00", true)]
    [Arguments("010041000B00", false)]
    [Arguments("010041000B00", true)]
    public async Task 空またはアクティブ要素のsectionがある_両入口で要素sectionの未対応を維持する(
        string payload,
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(9, Convert.FromHexString(payload))
        );
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("section.element");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 8, null, 9));
        }
    }
}
