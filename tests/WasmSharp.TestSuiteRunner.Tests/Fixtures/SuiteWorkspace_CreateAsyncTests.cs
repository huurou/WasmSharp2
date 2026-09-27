namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal class SuiteWorkspace_CreateAsyncTests
{
    [Test]
    public async Task 異なる一時領域に作成する_固定commitと素材を保ち出力を独立させる()
    {
        // Arrange
        using var first = await SuiteWorkspace.CreateAsync();

        // Act
        using var second = await SuiteWorkspace.CreateAsync();
        await File.WriteAllTextAsync(Path.Combine(first.OutputRoot, "result.json"), "{}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Commit.Length).IsEqualTo(40);
            await Assert.That(second.Commit).IsEqualTo(first.Commit);
            await Assert.That(second.SourceRoot).IsNotEqualTo(first.SourceRoot);
            await Assert.That(second.OutputRoot).IsNotEqualTo(first.OutputRoot);
            await Assert.That(Directory.GetFiles(second.OutputRoot)).IsEmpty();
            await Assert
                .That(await first.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
            await Assert
                .That(await second.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
            await Assert
                .That(await first.GitAsync("show", "HEAD:test/core/success.wast"))
                .IsEqualTo("(module)\n");
        }
    }
}
