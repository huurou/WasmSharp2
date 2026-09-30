using System.Diagnostics;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal class Program_MainTests
{
    [Test]
    public async Task ヘルプを指定してビルド済みCLIを直接起動する_説明を表示して正常終了する()
    {
        // Arrange
        var executablePath = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows()
                ? "WasmSharp.TestSuiteRunner.exe"
                : "WasmSharp.TestSuiteRunner"
        );
        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add("--help");

        // Act
        var result = await TestProcess.RunAsync(startInfo);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.Output).Contains("Test Suite Runner");
            await Assert.That(result.Output).Contains("compare-run --baseline");
            await Assert.That(result.Error).IsEqualTo(string.Empty);
        }
    }

    [Test]
    [Arguments("verify")]
    [Arguments("baseline-save")]
    public async Task ビルド済みCLIへ保存済み結果を渡す_操作を実行して所定の終了値を返す(
        string operation
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        var input = directory.Combine("run.json");
        var output = directory.Combine("baseline.json");
        WasmSharp.TestSuiteRunner.Reports.ReportStore.Save(report, input);
        var executablePath = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows()
                ? "WasmSharp.TestSuiteRunner.exe"
                : "WasmSharp.TestSuiteRunner"
        );
        var startInfo = new ProcessStartInfo(executablePath) { WorkingDirectory = directory.Root };
        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add("run.json");
        if (operation == "baseline-save")
        {
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add("baseline.json");
        }

        // Act
        var result = await TestProcess.RunAsync(startInfo);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(operation == "verify" ? 1 : 0);
            await Assert.That(result.Error).IsEqualTo(string.Empty);
            await Assert
                .That(result.Output)
                .Contains(operation == "verify" ? "最終判定: 不合格" : "baseline保存: 完了");
            if (operation == "baseline-save")
            {
                await Assert
                    .That(File.ReadAllBytes(output))
                    .IsEquivalentTo(File.ReadAllBytes(input));
            }
        }
    }
}
