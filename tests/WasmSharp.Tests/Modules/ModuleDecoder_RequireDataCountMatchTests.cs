using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules;

internal class ModuleDecoder_RequireDataCountMatchTests
{
    [Test]
    [Arguments(null, 0u)]
    [Arguments(null, 3u)]
    [Arguments(0u, 0u)]
    [Arguments(3u, 3u)]
    public async Task データ数宣言がないか実数と一致する_受理する(
        uint? declaredCount,
        uint segmentCount
    )
    {
        // Arrange
        DataCountDefinition? declaration = declaredCount is { } count ? new(count, 40) : null;

        // Act & Assert
        await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader([], 80, 11);
                ModuleDecoder.RequireDataCountMatch(ref reader, declaration, segmentCount);
            })
            .ThrowsNothing();
    }

    [Test]
    [Arguments(1u, 0u)]
    [Arguments(0u, 1u)]
    [Arguments(3u, 2u)]
    public async Task 宣言したデータ数と実数が一致しない_読了位置でDecode失敗を報告する(
        uint declaredCount,
        uint segmentCount
    )
    {
        // Arrange
        var declaration = new DataCountDefinition(declaredCount, 40);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader([], 80, 11);
                ModuleDecoder.RequireDataCountMatch(ref reader, declaration, segmentCount);
            })
            .ThrowsExactly<WasmDecodeException>();

        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "data count and data section have inconsistent lengths",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 80, null, 11));
        }
    }
}
