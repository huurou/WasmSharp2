namespace WasmSharp.Tests;

internal class WasmMemory_TryReadTests
{
    [Test]
    [Arguments(65535UL, 2)]
    [Arguments(65537UL, 0)]
    public async Task 範囲外から読み出す_転送先を変更せずfalseを返す(ulong offset, int length)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        var destination = new byte[length];
        Array.Fill(destination, (byte)42);

        // Act
        var success = memory.TryRead(offset, destination);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(destination.All(x => x == 42)).IsTrue();
        }
    }
}
