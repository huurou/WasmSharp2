using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class EnvironmentCapture_CaptureRuntimeConfigurationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Runtimeconfigのpathが不明かファイルがない_取得不能の理由を記録してコピーは作らない(bool hasPath)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, new CaseJson());
        var path = hasPath ? Path.Combine(fixture.DirectoryPath, "missing.runtimeconfig.json") : null;

        // Act
        var setting = EnvironmentCapture.CaptureRuntimeConfiguration(store, path).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(setting.Name).IsEqualTo("RuntimeConfig");
            await Assert.That(setting.Value).StartsWith("Unavailable:").And.Contains("runtimeconfigが見つかりません");
            await Assert
                .That(File.Exists(Path.Combine(fixture.DirectoryPath, "settings", "collector.runtimeconfig.json")))
                .IsFalse();
        }
    }
}
