using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetMemoryTests
{
    [Test]
    [Arguments("missing")]
    [Arguments("run")]
    [Arguments("")]
    public async Task 指定した種類のexportがない_名前の引数不正として拒否する(string name)
    {
        // Arrange
        var instance = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetMemory(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
