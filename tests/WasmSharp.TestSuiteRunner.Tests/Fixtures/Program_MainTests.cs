using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal class Program_MainTests
{
    [Test]
    public async Task 出力先の親ディレクトリが存在しない_自動作成せず保存失敗を返す()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var parentPath = Path.Combine(workspace.OutputRoot, "未作成");
        var outputPath = Path.Combine(parentPath, "result.json");

        // Act
        var result = await workspace.ConvertAsync("success.wast", outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains(outputPath);
            await Assert.That(result.Error).Contains("出力に失敗しました");
            await Assert.That(Directory.Exists(parentPath)).IsFalse();
            await Assert.That(Directory.GetFiles(workspace.OutputRoot)).IsEmpty();
        }
    }

    [Test]
    public async Task 出力先の親が既存ファイルである_既存内容を保持して保存失敗を返す()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var resultFixture = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "incomplete-run-report.json")
        );
        var existingPath = Path.Combine(workspace.OutputRoot, "既存結果.json");
        await File.WriteAllBytesAsync(existingPath, resultFixture);
        var outputPath = Path.Combine(existingPath, "result.json");

        // Act
        var result = await workspace.ConvertAsync("success.wast", outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains(outputPath);
            await Assert.That(result.Error).Contains("出力に失敗しました");
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(existingPath)))
                .IsEqualTo(Convert.ToHexString(resultFixture));
            await Assert.That(File.Exists(outputPath)).IsFalse();
            await Assert
                .That(await workspace.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
        }
    }

    [Test]
    [Arguments("partial.wast", 0)]
    [Arguments("partial-failure.wast", 1)]
    public async Task 部分生成入力を指定する_終了値にかかわらずJSONだけを残す(
        string inputName,
        int exitCode
    )
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var outputPath = Path.Combine(workspace.OutputRoot, "partial.json");

        // Act
        var result = await workspace.ConvertAsync(inputName, outputPath);

        // Assert
        using var script = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        var moduleName = script
            .RootElement.GetProperty("commands")[0]
            .GetProperty("filename")
            .GetString()!;
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(exitCode);
            await Assert.That(result.Error).Contains("JSONのみ生成しました");
            await Assert
                .That(File.Exists(Path.Combine(workspace.OutputRoot, moduleName)))
                .IsFalse();
            await Assert.That(Directory.GetFiles(workspace.OutputRoot)).Count().IsEqualTo(1);
            await Assert
                .That(await workspace.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task 失敗入力を指定する_素材を生成せず診断と非0終了値を返す()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var outputPath = Path.Combine(workspace.OutputRoot, "failure.json");

        // Act
        var result = await workspace.ConvertAsync("failure.wast", outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(result.Error).Contains("failure.wastの変換に失敗しました");
            await Assert.That(result.Output).Contains("failure.wast");
            await Assert.That(Directory.GetFiles(workspace.OutputRoot)).IsEmpty();
            await Assert
                .That(await workspace.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task 成功入力を指定する_JSONと有効なbinaryを生成して実プロセスが正常終了する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var outputPath = Path.Combine(workspace.OutputRoot, "結果.json");

        // Act
        var result = await workspace.ConvertAsync("success.wast", outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.Error).IsEqualTo(string.Empty);
            await Assert.That(File.Exists(outputPath)).IsTrue();
        }

        using var arguments = JsonDocument.Parse(result.Output);
        using var script = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        var moduleName = script
            .RootElement.GetProperty("commands")[0]
            .GetProperty("filename")
            .GetString()!;
        var binary = await File.ReadAllBytesAsync(Path.Combine(workspace.OutputRoot, moduleName));
        using (Assert.Multiple())
        {
            await Assert
                .That(arguments.RootElement.GetProperty("arguments").GetArrayLength())
                .IsEqualTo(3);
            await Assert
                .That(arguments.RootElement.GetProperty("arguments")[2].GetString())
                .IsEqualTo(outputPath);
            await Assert
                .That(arguments.RootElement.GetProperty("working_directory").GetString())
                .IsEqualTo(workspace.InputRoot);
            await Assert
                .That(script.RootElement.GetProperty("source_filename").GetString())
                .IsEqualTo("success.wast");
            await Assert.That(Convert.ToHexString(binary)).IsEqualTo("0061736D01000000");
            await Assert
                .That(() => WasmModule.Decode(binary).Validate().Instantiate(new WasmImports()))
                .ThrowsNothing();
            await Assert
                .That(await workspace.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
        }
    }
}
