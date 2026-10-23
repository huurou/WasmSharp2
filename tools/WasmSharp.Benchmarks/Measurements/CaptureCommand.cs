using System.Diagnostics;
using System.Text;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 採取に必要な読み取り専用コマンドの出力と失敗を取得する
/// </summary>
internal static class CaptureCommand
{
    internal static string Run(string executable, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var text = output.GetAwaiter().GetResult();
        var diagnostic = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(executable + "の採取に失敗しました: " + diagnostic);
        }

        return text;
    }
}
