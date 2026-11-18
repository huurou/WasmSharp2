using System.Text;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class CaptureCommand_RunBytesTests
{
    [Test]
    public async Task 複数の値をNUL区切りで出力する_長い値と終端も含めて全バイトを返す()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        Directory.CreateDirectory(fixture.DirectoryPath);
        const string First = "最初の値";
        var last = new string('x', 128 * 1024) + "末尾";
        var config = Path.Combine(fixture.DirectoryPath, "values.gitconfig");
        File.WriteAllText(config, $"[capture]\n\tvalue = {First}\n\tvalue = {last}\n");
        var expected = Encoding.UTF8.GetBytes($"{First}\0{last}\0");

        // Act
        var actual = CaptureCommand.RunBytes(
            "git",
            fixture.DirectoryPath,
            "config",
            "--null",
            "--file",
            config,
            "--get-all",
            "capture.value"
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Length).IsEqualTo(expected.Length);
            await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
        }
    }

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
