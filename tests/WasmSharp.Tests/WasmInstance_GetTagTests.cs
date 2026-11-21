using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetTagTests
{
    [Test]
    public async Task タグ取得を呼び出す_未対応を示す例外を送出する()
    {
        // Arrange
        var instance = WasmModule
            .Decode(HostLinkingModuleBinary.Create(), WasmSpecVersion.Core3)
            .Validate()
            .Instantiate(new WasmExecutionContext(), []);

        // Act & Assert
        await Assert.That(() => instance.GetTag("tag")).ThrowsExactly<NotImplementedException>();
    }
}
