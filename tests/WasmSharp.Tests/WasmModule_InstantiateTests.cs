using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_InstantiateTests
{
    [Test]
    public async Task 検証していない_状態の契約違反として拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B));

        // Act & Assert
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }
}
