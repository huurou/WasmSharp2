using System.Text;

namespace WasmSharp.Tests.Fixtures;

internal class HostLinkingModuleBinary_ExportsTests
{
    [Test]
    public async Task 単独サロゲートの名前を指定する_代替文字へ置換せず拒否する()
    {
        // Arrange
        string[] names = ["\uD800", "\uDC00"];

        // Act & Assert
        foreach (var name in names)
        {
            await Assert
                .That(() => HostLinkingModuleBinary.Exports((name, 0, 0)))
                .ThrowsExactly<EncoderFallbackException>();
        }
    }
}
