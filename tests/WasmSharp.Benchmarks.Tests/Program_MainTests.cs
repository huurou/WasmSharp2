using System.Diagnostics;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests;

internal class Program_MainTests
{
    [Test]
    public async Task ビルドしたCLIに未知の引数を渡す_標準エラーに引数名を出力して終了値2を返す()
    {
        // Arrange
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
        start.ArgumentList.Add(typeof(BenchmarkCli).Assembly.Location);
        start.ArgumentList.Add("check");
        start.ArgumentList.Add("--unknown-option");

        // Act
        var result = await TestProcess.RunAsync(start);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Output).IsEmpty();
            await Assert.That(result.Error).Contains("--unknown-option");
        }
    }
}
