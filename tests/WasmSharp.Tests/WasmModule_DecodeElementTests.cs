using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("00", false)]
    [Arguments("00", true)]
    [Arguments("010041000B00", false)]
    [Arguments("010041000B00", true)]
    public async Task 空またはアクティブ要素のsectionがある_両入口で要素定義と入力位置を保持する(
        string payload,
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(9, Convert.FromHexString(payload))
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.ElementSegments.Length).IsEqualTo(payload == "00" ? 0 : 1);
            if (payload == "00")
            {
                await Assert
                    .That(module.Validate().Instantiate([]).ElementSegments.IsEmpty)
                    .IsTrue();
            }
            else
            {
                await Assert.That(module.ElementSegments[0].ByteOffset).IsEqualTo(11L);
            }
        }
    }
}
