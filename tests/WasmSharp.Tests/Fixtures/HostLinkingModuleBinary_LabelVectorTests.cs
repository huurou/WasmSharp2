namespace WasmSharp.Tests.Fixtures;

internal class HostLinkingModuleBinary_LabelVectorTests
{
    [Test]
    public async Task 空と複数のラベルを指定する_候補数と宣言順とuint最大値を保持する()
    {
        // Arrange
        uint[] labels = [0, 128, uint.MaxValue];

        // Act
        var empty = HostLinkingModuleBinary.LabelVector();
        var actual = HostLinkingModuleBinary.LabelVector(labels);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Convert.ToHexString(empty)).IsEqualTo("00");
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo("03008001FFFFFFFF0F");
        }
    }
}
