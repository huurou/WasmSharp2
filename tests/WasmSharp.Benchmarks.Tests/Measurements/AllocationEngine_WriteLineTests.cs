using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class AllocationEngine_WriteLineTests
{
    [Test]
    [Arguments(null)]
    [Arguments("測定ログ")]
    public async Task 行出力を要求する_空行または内容を時間計測engineへ一度だけ渡す(string? line)
    {
        // Arrange
        var timing = new AllocationEngineFixture();
        using var engine = new AllocationEngine(timing, null, null);

        // Act
        if (line is null)
        {
            engine.WriteLine();
        }
        else
        {
            engine.WriteLine(line);
        }

        // Assert
        await Assert.That(timing.WrittenLines.Single()).IsEqualTo(line);
    }
}
