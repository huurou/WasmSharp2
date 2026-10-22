using System.Text.Json;
using System.Text.Json.Nodes;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class ComparisonPolicy_DeserializeTests
{
    [Test]
    public async Task 比較fixtureを往復保存する_閾値の単位と個別判定と零基準のnull比率を保持する()
    {
        // Arrange
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "comparison.json")
        );
        var fixture = JsonSerializer.Deserialize<ComparisonFixture>(json, CaseJson.Options)!;
        using var stream = new MemoryStream();

        // Act
        await JsonSerializer.SerializeAsync(stream, fixture, CaseJson.Options);
        stream.Position = 0;
        var restored = await JsonSerializer.DeserializeAsync<ComparisonFixture>(
            stream,
            CaseJson.Options
        );
        var actual = JsonSerializer.SerializeToNode(restored, CaseJson.Options);

        // Assert
        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), actual)).IsTrue();
    }

    private sealed record ComparisonFixture(
        ComparisonPolicy Policy,
        ComparisonRequest Request,
        ComparisonReport Report
    );
}
