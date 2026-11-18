using System.Text.Json;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal class RunnerCli_GenerateTests
{
    [Test]
    public async Task Core3を指定して生成する_公式commandとテキストおよびバイナリ素材を保持する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, "gc/sample.wast", "(module)\n");

        // Act
        var (exitCode, error) = fixture.Generate("generated");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(0).Because(error);
            var modulePath = directory.Combine("generated/modules/gc");
            var raw = await File.ReadAllBytesAsync(Path.Combine(modulePath, "sample.json"));
            var expected = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/Data/core3-script.json"));
            await Assert.That(raw.SequenceEqual(expected)).IsTrue();
            await Assert.That(Convert.ToHexString(await File.ReadAllBytesAsync(Path.Combine(modulePath, "sample.1.wasm")))).IsEqualTo("006173");
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(modulePath, "sample.3.wat"))).IsEqualTo("(module ?)");
            using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(directory.Combine("generated/manifest.json")));
            var release = manifest.RootElement.GetProperty("profile").GetProperty("wasm_tools");
            await Assert.That(release.GetProperty("version").GetString()).IsEqualTo("1.263.0");
            await Assert.That(release.GetProperty("commit").GetString()).IsEqualTo("7a2fa422cdf9aeeaf488a2acac8bc94a078bfa32");
            var input = manifest.RootElement.GetProperty("inputs")[0];
            await Assert.That(input.GetProperty("status").GetString()).IsEqualTo("succeeded");
            var script = input.GetProperty("artifacts").EnumerateArray().Single(x => x.GetProperty("kind").GetString() == "json").GetProperty("script");
            await Assert.That(script.GetProperty("command_count").GetInt32()).IsEqualTo(7);
            await Assert.That(script.GetProperty("commands")[5].GetProperty("module_type").GetString()).IsEqualTo("text");
            await Assert.That(script.GetProperty("references").EnumerateArray().Select(x => x.GetProperty("artifact_path").GetString()!))
                .IsEquivalentTo(["modules/gc/sample.0.wasm", "modules/gc/sample.1.wasm", "modules/gc/sample.2.wat", "modules/gc/sample.2.wasm", "modules/gc/sample.3.wat"]);
        }
    }

    [Test]
    [Arguments("gc/sample.wast", "missing companion", 0, "記録されていない素材")]
    [Arguments("failure.wast", "(module)\n", 1, "変換器が終了値1")]
    public async Task Core3の素材が欠落する_対象入力を残して変換失敗として記録する(string name, string source, int converterExitCode, string diagnostic)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, name, source);

        // Act
        var (exitCode, error) = fixture.Generate("generated");

        // Assert
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(directory.Combine("generated/manifest.json")));
        var input = manifest.RootElement.GetProperty("inputs")[0];
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(error).Contains(diagnostic);
            await Assert.That(input.GetProperty("input").GetProperty("path").GetString()).IsEqualTo(name);
            await Assert.That(input.GetProperty("status").GetString()).IsEqualTo("runner_error");
            await Assert.That(input.GetProperty("exit_code").GetInt32()).IsEqualTo(converterExitCode);
        }
    }
}
