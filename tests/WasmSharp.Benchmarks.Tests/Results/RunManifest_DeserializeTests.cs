using System.Text.Json;
using System.Text.Json.Nodes;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunManifest_DeserializeTests
{
    [Test]
    [Arguments("run-succeeded.json")]
    [Arguments("run-failed.json")]
    public async Task runのfixtureを往復保存する_成否と指標と同定情報を保持する(string name)
    {
        // Arrange
        var json = await ReadFixtureAsync(name);
        var fixture = JsonSerializer.Deserialize<RunFixture>(json, CaseJson.Options)!;
        using var stream = new MemoryStream();

        // Act
        await JsonSerializer.SerializeAsync(stream, fixture, CaseJson.Options);
        stream.Position = 0;
        var restored = await JsonSerializer.DeserializeAsync<RunFixture>(stream, CaseJson.Options);
        var actual = JsonSerializer.SerializeToNode(restored, CaseJson.Options);

        // Assert
        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), actual)).IsTrue();
    }

    [Test]
    [Arguments("time_unavailable_reason")]
    [Arguments("allocation_unavailable_reason")]
    [Arguments("gc_unavailable_reason")]
    public async Task 指標がnullなのに理由がない_取得不能の説明を省略できない(string property)
    {
        // Arrange
        var fixture = JsonNode.Parse(await ReadFixtureAsync("run-failed.json"))!;
        fixture["run"]!["cases"]![0]!["metrics"]![property] = null;

        // Act & Assert
        await Assert
            .That(() =>
                JsonSerializer.Deserialize<RunFixture>(fixture.ToJsonString(), CaseJson.Options)
            )
            .ThrowsExactly<ArgumentException>();
    }

    private static Task<string> ReadFixtureAsync(string name)
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", name)
        );
    }

    private sealed record RunFixture(
        RunManifest Run,
        RunEnvironment? Environment,
        RunSource? Source,
        List<ProcessCheck> Checks
    );
}
