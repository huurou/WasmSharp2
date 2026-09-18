namespace WasmSharp.Tests;

internal class WasmMemory_ConstructorTests
{
    [Test]
    [Arguments(2u, 1u)]
    [Arguments(0u, 65537u)]
    [Arguments(65537u, null)]
    public async Task Limitsが不正である_割当前に契約違反として拒否する(uint minimum, uint? maximum)
    {
        // Arrange
        var limits = new WasmLimits(minimum, maximum);

        // Act & Assert
        await Assert.That(() => new WasmMemory(limits)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Limitsがnullである_引数例外として拒否する()
    {
        // Act & Assert
        await Assert.That(() => new WasmMemory(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments(0u, null)]
    [Arguments(2u, 3u)]
    [Arguments(0u, 65536u)]
    public async Task 有効なlimitsで生成する_現在サイズと最大値を保持してゼロ初期化する(
        uint minimum,
        uint? maximum
    )
    {
        // Arrange
        var limits = new WasmLimits(minimum, maximum);
        var bytes = new byte[minimum == 0 ? 0 : 131072];
        Array.Fill(bytes, (byte)255);

        // Act
        var memory = new WasmMemory(limits);
        memory.Read(0, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(memory.PageCount).IsEqualTo(minimum);
            await Assert.That(memory.MaximumPages).IsEqualTo(maximum);
            await Assert.That(memory.ByteLength).IsEqualTo(minimum == 0 ? 0UL : 131072UL);
            await Assert.That(bytes.All(x => x == 0)).IsTrue();
        }
    }
}
