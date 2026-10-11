namespace WasmSharp.Tests.Fixtures;

internal class HostLinkingModuleBinary_TypeVectorTests
{
    [Test]
    [Arguments("", "00")]
    [Arguments("7F", "017F")]
    [Arguments("7F7E7D7C7B706F", "077F7E7D7C7B706F")]
    [Arguments("00FF", "0200FF")]
    public async Task 型列を指定する_空と単一と全値型と不正な型を補正せず符号化する(
        string types,
        string expected
    )
    {
        // Arrange
        var values = Convert.FromHexString(types);

        // Act
        var actual = HostLinkingModuleBinary.TypeVector(values);

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo(expected);
    }
}
