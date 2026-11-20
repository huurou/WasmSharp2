using System.Text.Json.Nodes;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    [Arguments("wabt", false)]
    [Arguments("conversion", false)]
    [Arguments("default_enabled", false)]
    [Arguments("wabt", true)]
    [Arguments("conversion", true)]
    [Arguments("default_enabled", true)]
    public async Task WABTの保存条件に不足または別の変換器がある_変換比較は読取を拒否する(string field, bool hasWasmTools)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var input = directory.Combine("manifest.json");
        var comparison = directory.Combine("comparison.json");
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1));
        _reportStore.Save(manifest with { Profile = manifest.Profile with { Id = "core2" } }, input);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(input))!;
        var profile = json["profile"]!;
        if (hasWasmTools)
        {
            profile["wasm_tools"] = JsonNode.Parse(
                """
                {"url":"https://github.com/bytecodealliance/wasm-tools.git","commit":"7a2fa422cdf9aeeaf488a2acac8bc94a078bfa32","version":"1.263.0"}
                """
            );
        }
        var parent =
            field == "default_enabled"
                ? profile["features"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "simd")!
                : profile;
        var expectedField = field == "default_enabled" ? "features[simd].default_enabled" : field;
        parent[field] = null;
        await File.WriteAllTextAsync(input, json.ToJsonString());

        // Act
        var result = Run(
            _profileLoader.Load(),
            "compare-conversion",
            "--baseline",
            input,
            "--current",
            input,
            "--output",
            comparison
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("保存済み結果のJSONとして読み取れません");
            await Assert
                .That(result.Error)
                .Contains(hasWasmTools ? "wasm_toolsとWABTの変換条件は併用できません" : $"{expectedField}が必要です。");
            await Assert.That(File.Exists(comparison)).IsFalse();
        }
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("url", "https://mirror.invalid/wasm-tools.git")]
    [Arguments("commit", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Arguments("version", "1.263.1")]
    public async Task Core3を別の配置に再生成して比較する_変換器の取得元とcommitと版の差を報告する(
        string? field,
        string? changedValue
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, "gc/sample.wast", "(module)\n");
        var before = fixture.Generate("before");
        var after = fixture.Generate("after");
        await Assert.That(before.ExitCode).IsEqualTo(0).Because(before.Error);
        await Assert.That(after.ExitCode).IsEqualTo(0).Because(after.Error);
        var current = directory.Combine("after/manifest.json");
        string? originalValue = null;
        if (field is not null)
        {
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(current))!;
            var release = manifest["profile"]!["wasm_tools"]!;
            originalValue = release[field]!.GetValue<string>();
            release[field] = changedValue;
            await File.WriteAllTextAsync(current, manifest.ToJsonString());
        }
        var comparison = directory.Combine("comparison.json");

        // Act
        var (exitCode, error) = fixture.Run([
            "compare-conversion",
            "--baseline",
            directory.Combine("before/manifest.json"),
            "--current",
            current,
            "--output",
            comparison,
        ]);

        // Assert
        var report = JsonNode.Parse(await File.ReadAllTextAsync(comparison))!;
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(field is null ? 0 : 1).Because(error);
            await Assert.That(report["complete"]!.GetValue<bool>()).IsTrue();
            await Assert.That(report["entry_differences"]!.AsArray()).IsEmpty();
            var conditions = report["condition_differences"]!.AsArray();
            if (field is not null)
            {
                var difference = conditions.Single()!;
                await Assert.That(difference["name"]!.GetValue<string>()).IsEqualTo($"wasm_tools.{field}");
                await Assert.That(difference["baseline"]!.GetValue<string>()).IsEqualTo(originalValue);
                await Assert.That(difference["current"]!.GetValue<string>()).IsEqualTo(changedValue);
            }
            else
            {
                await Assert.That(conditions).IsEmpty();
            }
        }
    }
}
