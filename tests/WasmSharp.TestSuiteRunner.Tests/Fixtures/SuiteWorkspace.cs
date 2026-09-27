using System.Diagnostics;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal sealed class SuiteWorkspace : IDisposable
{
    private readonly DirectoryInfo root_;

    internal string SourceRoot => Path.Combine(root_.FullName, "source");

    internal string InputRoot => Path.Combine(SourceRoot, "test", "core");

    internal string OutputRoot => Path.Combine(root_.FullName, "出力 先");

    internal string Commit { get; private set; } = string.Empty;

    internal static string ConverterPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "ConverterFixture",
            OperatingSystem.IsWindows() ? "ConverterFixture.exe" : "ConverterFixture"
        );

    private SuiteWorkspace()
    {
        root_ = Directory.CreateTempSubdirectory("WasmSharp.TestSuiteRunner.Tests ");
    }

    internal static async Task<SuiteWorkspace> CreateAsync()
    {
        var workspace = new SuiteWorkspace();
        try
        {
            Directory.CreateDirectory(workspace.InputRoot);
            Directory.CreateDirectory(workspace.OutputRoot);
            Directory.CreateDirectory(Path.Combine(workspace.root_.FullName, "empty-template"));
            foreach (var name in new[] { "success", "failure", "partial", "partial-failure" })
            {
                await File.WriteAllTextAsync(
                    Path.Combine(workspace.InputRoot, name + ".wast"),
                    "(module)\n"
                );
            }

            await workspace.GitAsync(
                "init",
                "--object-format=sha1",
                "--initial-branch=main",
                "--template=" + Path.Combine(workspace.root_.FullName, "empty-template"),
                "."
            );
            await workspace.GitAsync("add", "--", "test/core");
            await workspace.GitAsync("commit", "--no-verify", "-m", "検証用の固定入力");
            workspace.Commit = (await workspace.GitAsync("rev-parse", "HEAD")).Trim();
            return workspace;
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    internal async Task<string> GitAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = SourceRoot };
        foreach (
            var key in startInfo
                .Environment.Keys.Where(x => x.StartsWith("GIT_", StringComparison.Ordinal))
                .ToArray()
        )
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(
            root_.FullName,
            "empty-gitconfig"
        );
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Test Suite Runner";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "fixture@example.invalid";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Test Suite Runner";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "fixture@example.invalid";
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2000-01-01T00:00:00+00:00";
        startInfo.Environment["GIT_COMMITTER_DATE"] = "2000-01-01T00:00:00+00:00";
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("core.autocrlf=false");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var result = await TestProcess.RunAsync(startInfo);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"一時Git素材の準備・確認に失敗しました: {result.Error}"
            );
        }

        return result.Output;
    }

    internal Task<ProcessResult> ConvertAsync(string inputName, string outputPath)
    {
        var startInfo = new ProcessStartInfo(ConverterPath) { WorkingDirectory = InputRoot };
        startInfo.ArgumentList.Add(inputName);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);
        return TestProcess.RunAsync(startInfo);
    }

    public void Dispose()
    {
        // このfixtureが作成した一時領域内のGit objectには読み取り専用属性が付く。
        foreach (var file in root_.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }

        root_.Delete(recursive: true);
    }
}
