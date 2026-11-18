using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class CaptureCommand_RunBytesTests
{
    [Test]
    public async Task コマンドが非零で終了する_部分出力を成功として返さない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        Directory.CreateDirectory(fixture.DirectoryPath);

        // Act & Assert
        await Assert
            .That(() => CaptureCommand.RunBytes("git", fixture.DirectoryPath, "wasmsharp-nonexistent-command"))
            .ThrowsExactly<InvalidOperationException>();
    }
}
