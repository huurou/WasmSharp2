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
        using var process = Start(executable, directory, arguments);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var text = output.GetAwaiter().GetResult();
        EnsureSucceeded(process, executable, error);
        return text;
    }

    internal static byte[] RunBytes(string executable, string directory, params string[] arguments)
    {
        using var process = Start(executable, directory, arguments);
        using var output = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        copy.GetAwaiter().GetResult();
        EnsureSucceeded(process, executable, error);
        return output.ToArray();
    }

    private static Process Start(string executable, string directory, string[] arguments)
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

        return Process.Start(start)!;
    }

    private static void EnsureSucceeded(Process process, string executable, Task<string> error)
    {
        var diagnostic = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(executable + "の採取に失敗しました: " + diagnostic);
        }
    }
}
