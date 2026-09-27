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
            await Assert.That(result.Error).IsEqualTo(string.Empty);
        }
    }
}
