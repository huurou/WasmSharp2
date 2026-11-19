using TUnit.Core.Enums;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class CaptureCommand_RunTests
{
    [Test]
    [RunOn(OS.Windows)]
    public async Task Windowsで採取する_子にコンソールウィンドウを開かず呼出元のコンソールも共有しない()
    {
        // Arrange
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell/v1.0/powershell.exe");
        const string Command = """
            $ErrorActionPreference = 'Stop'
            Add-Type -Namespace WasmSharp -Name ConsoleProbe -MemberDefinition @'
            [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
            [DllImport("kernel32.dll")] public static extern uint GetConsoleProcessList(uint[] processes, uint count);
            '@
            $window = [WasmSharp.ConsoleProbe]::GetConsoleWindow()
            $count = [WasmSharp.ConsoleProbe]::GetConsoleProcessList([uint32[]]::new(1), 1)
            [Console]::WriteLine($window -eq [IntPtr]::Zero -and $count -le 1)
            """;

        // Act
        var result = CaptureCommand.Run(
            powershell,
            AppContext.BaseDirectory,
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            Command
        );

        // Assert
        await Assert.That(result.Trim()).IsEqualTo("True");
    }

    [Test]
    public async Task コマンドが非零で終了する_標準エラーを含む採取失敗を返す()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        Directory.CreateDirectory(fixture.DirectoryPath);

        // Act & Assert
        var exception = await Assert
            .That(() => CaptureCommand.Run("git", fixture.DirectoryPath, "wasmsharp-nonexistent-command"))
            .ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("gitの採取に失敗しました: ");
            await Assert.That(exception.Message).Contains("wasmsharp-nonexistent-command");
        }
    }
}
