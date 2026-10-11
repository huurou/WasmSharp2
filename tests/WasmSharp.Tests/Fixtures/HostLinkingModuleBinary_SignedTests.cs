namespace WasmSharp.Tests.Fixtures;

internal class HostLinkingModuleBinary_SignedTests
{
    [Test]
    [Arguments(0L, "00")]
    [Arguments(63L, "3F")]
    [Arguments(64L, "C000")]
    [Arguments(-1L, "7F")]
    [Arguments(-64L, "40")]
    [Arguments(-65L, "BF7F")]
    [Arguments((long)int.MinValue, "8080808078")]
    [Arguments((long)int.MaxValue, "FFFFFFFF07")]
    [Arguments((long)uint.MaxValue, "FFFFFFFF0F")]
    [Arguments(long.MinValue, "8080808080808080807F")]
    [Arguments(long.MaxValue, "FFFFFFFFFFFFFFFFFF00")]
    public async Task 符号付き整数の幅境界を指定する_定数とs33型添字の符号を保持する(
        long value,
        string expected
    )
    {
        // Arrange
        var input = value;

        // Act
        var actual = HostLinkingModuleBinary.Signed(input);

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo(expected);
    }
}
