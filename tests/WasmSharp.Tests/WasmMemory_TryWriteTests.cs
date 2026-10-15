namespace WasmSharp.Tests;

internal class WasmMemory_TryWriteTests
{
    [Test]
    [Arguments(65535UL, 2)]
    [Arguments(65537UL, 0)]
    public async Task 範囲外へ書き込む_内容を変更せずfalseを返す(ulong offset, int length)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        memory.Write(65535, [42]);
        var source = new byte[length];
        Array.Fill(source, (byte)99);
        var actual = new byte[1];

        // Act
        var success = memory.TryWrite(offset, source);
        memory.Read(65535, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(actual[0]).IsEqualTo((byte)42);
        }
    }
}
