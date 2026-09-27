using System.Diagnostics;
using System.Text;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class TestProcess
{
    internal static async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // 検証用プロセスの異常でテスト全体が待ち続けることを防ぐ。
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"検証用プロセスが終了しませんでした: {startInfo.FileName}\n{await error}"
            );
        }

        return new ProcessResult(process.ExitCode, await output, await error);
    }
}

internal sealed record ProcessResult(int ExitCode, string Output, string Error);
