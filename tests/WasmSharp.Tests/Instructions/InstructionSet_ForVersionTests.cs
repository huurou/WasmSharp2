using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Instructions;

internal class InstructionSet_ForVersionTests
{
    [Test]
    [Arguments(WasmSpecVersion.Core2, 183, 0, 236)]
    [Arguments(WasmSpecVersion.Core3, 194, 31, 256)]
    public async Task 固定した仕様版の命令表を取得する_割り当てられた全番号と命令名が公式付録と一致する(
        WasmSpecVersion version,
        int normalCount,
        int gcCount,
        int simdCount
    )
    {
        // Arrange
        var expected = InstructionAppendix.Read(version);

        // Act
        var actual = InstructionSet.ForVersion(version).ToDictionary(x => x.Key, x => x.Value.Name);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(expected.Keys.Count(x => x.Prefix == 0)).IsEqualTo(normalCount);
            await Assert.That(expected.Keys.Count(x => x.Prefix == 0xFB)).IsEqualTo(gcCount);
            await Assert.That(expected.Keys.Count(x => x.Prefix == 0xFC)).IsEqualTo(18);
            await Assert.That(expected.Keys.Count(x => x.Prefix == 0xFD)).IsEqualTo(simdCount);
            await Assert.That(actual).IsEquivalentTo(expected);
        }
    }
}
