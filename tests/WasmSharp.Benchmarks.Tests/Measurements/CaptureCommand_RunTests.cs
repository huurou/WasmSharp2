using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class CaptureCommand_RunTests
{
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
