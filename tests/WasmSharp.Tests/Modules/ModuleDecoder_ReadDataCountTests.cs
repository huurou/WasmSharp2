using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleDecoder_ReadDataCountTests
{
    [Test]
    [Arguments("8000", 0u)]
    [Arguments("03", 3u)]
    [Arguments("FFFFFFFF0F", uint.MaxValue)]
    public async Task データ数がu32である_値と宣言位置を保持する(string hex, uint expectedCount)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        var reader = new ModuleBinaryReader(bytes, 40, 12);

        // Act
        var declaration = ModuleDecoder.ReadDataCount(ref reader);
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(declaration.Count).IsEqualTo(expectedCount);
            await Assert.That(declaration.ByteOffset).IsEqualTo(40L);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }
}
