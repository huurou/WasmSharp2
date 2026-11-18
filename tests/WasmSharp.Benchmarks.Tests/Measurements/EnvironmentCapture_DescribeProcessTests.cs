using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Measurements;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class EnvironmentCapture_DescribeProcessTests
{
    private readonly EnvironmentCapture _environmentCapture = new EnvironmentCapture();

    [Test]
    public async Task Jobの識別子と環境変数を指定する_値を持つ末端の設定だけをJob設定へ保存する()
    {
        // Arrange
        var job = Job.Default.WithId("job-identity").WithEnvironmentVariable("DOTNET_PRIVATE_API_TOKEN", "private-token");

        // Act
        var process = _environmentCapture.DescribeProcess("Measurement", job);

        // Assert
        var settingNames = process.JobSettings.Select(x => x.Name);
        using (Assert.Multiple())
        {
            await Assert.That(process.JobId).IsEqualTo("job-identity");
            await Assert.That(settingNames.SequenceEqual(settingNames.Order(StringComparer.Ordinal))).IsTrue();
            await Assert
                .That(
                    process.JobSettings.Any(x =>
                        x.Name.EndsWith(".Id", StringComparison.Ordinal)
                        || x.Name.EndsWith(".EnvironmentVariables", StringComparison.Ordinal)
                    )
                )
                .IsFalse();
            await Assert.That(process.JobSettings.All(x => x.Name.Contains('.'))).IsTrue();
            await Assert
                .That(process.JobSettings.Any(x => x.Value.Contains("private-token", StringComparison.Ordinal)))
                .IsFalse();
            await Assert.That(process.EnvironmentVariables).IsEmpty();
        }
    }
}
