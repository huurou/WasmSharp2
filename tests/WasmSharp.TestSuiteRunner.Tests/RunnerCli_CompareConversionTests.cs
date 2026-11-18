using System.Text.Json.Nodes;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal class RunnerCli_CompareConversionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Core3を別の配置に再生成して比較する_変換器の版だけが異なる場合も識別する(bool changeVersion)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, "gc/sample.wast", "(module)\n");
        var before = fixture.Generate("before");
        var after = fixture.Generate("after");
        await Assert.That(before.ExitCode).IsEqualTo(0).Because(before.Error);
        await Assert.That(after.ExitCode).IsEqualTo(0).Because(after.Error);
        var current = directory.Combine("after/manifest.json");
        if (changeVersion)
        {
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(current))!;
            manifest["profile"]!["wasm_tools"]!["version"] = "1.263.1";
            await File.WriteAllTextAsync(current, manifest.ToJsonString());
        }
        var comparison = directory.Combine("comparison.json");

        // Act
        var (exitCode, error) = fixture.Run(["compare-conversion", "--baseline", directory.Combine("before/manifest.json"), "--current", current, "--output", comparison]);

        // Assert
        var report = JsonNode.Parse(await File.ReadAllTextAsync(comparison))!;
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(changeVersion ? 1 : 0).Because(error);
            await Assert.That(report["complete"]!.GetValue<bool>()).IsTrue();
            await Assert.That(report["entry_differences"]!.AsArray()).IsEmpty();
            var conditions = report["condition_differences"]!.AsArray();
            if (changeVersion)
            {
                await Assert.That(conditions.Single()!["name"]!.GetValue<string>()).IsEqualTo("wasm_tools.version");
            }
            else
            {
                await Assert.That(conditions).IsEmpty();
            }
        }
    }
}
