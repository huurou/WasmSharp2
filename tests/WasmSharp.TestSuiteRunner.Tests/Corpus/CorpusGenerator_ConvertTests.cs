using System.ComponentModel;
using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusGenerator_ConvertTests
{
    [Test]
    public async Task 成功する入力を変換する_相対入力と解決済みの絶対出力を渡し終了値と標準出力を記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());
        var input = new InputConversionResult(
            request.Profile.Inputs.Single(x => x.Path == "success.wast")
        );
        var outputPath = Path.Combine(workspace.OutputRoot, "modules", "success.json");

        // Act
        var result = CorpusGenerator.Convert(request, input);

        // Assert
        using var output = JsonDocument.Parse(result.StandardOutput);
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert
                .That(result.Arguments.SequenceEqual(["success.wast", "-o", outputPath]))
                .IsTrue();
            await Assert
                .That(
                    output
                        .RootElement.GetProperty("arguments")
                        .EnumerateArray()
                        .Select(x => x.GetString())
                        .SequenceEqual(result.Arguments)
                )
                .IsTrue();
            await Assert
                .That(output.RootElement.GetProperty("working_directory").GetString())
                .IsEqualTo(workspace.InputRoot);
            await Assert.That(result.StandardError).IsEqualTo(string.Empty);
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(File.Exists(outputPath)).IsTrue();
            await Assert
                .That(File.Exists(Path.Combine(workspace.OutputRoot, "modules", "success.0.wasm")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 失敗する入力を変換する_終了値と標準エラーを変換失敗の診断とともに記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());
        var input = new InputConversionResult(
            request.Profile.Inputs.Single(x => x.Path == "failure.wast")
        );

        // Act
        var result = CorpusGenerator.Convert(request, input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(result.StandardOutput).Contains("failure.wast");
            await Assert.That(result.StandardError).Contains("failure.wastの変換に失敗しました。");
            await Assert.That(result.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(result.Diagnostics[0].Operation).IsEqualTo("convert");
            await Assert.That(result.Diagnostics[0].Path).IsEqualTo("failure.wast");
        }
    }

    [Test]
    public async Task 出力先のディレクトリを用意できない_変換器を起動せず出力先の準備失敗を記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());
        var input = new InputConversionResult(
            request.Profile.Inputs.Single(x => x.Path == "success.wast")
        );
        await File.WriteAllTextAsync(Path.Combine(workspace.OutputRoot, "modules"), "既存ファイル");

        // Act
        var result = CorpusGenerator.Convert(request, input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert.That(result.ExitCode).IsNull();
            await Assert.That(result.StandardOutput).IsEqualTo(string.Empty);
            await Assert.That(result.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(result.Diagnostics[0].Message).Contains("出力先");
            await Assert
                .That(result.Diagnostics[0].ExceptionType)
                .IsEqualTo(typeof(IOException).FullName);
        }
    }

    [Test]
    public async Task 変換器を起動できない_終了値なしで起動失敗を記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile()) with
        {
            ConverterPath = Path.Combine(workspace.OutputRoot, "missing-wast2json"),
        };
        var input = new InputConversionResult(request.Profile.Inputs[0]);

        // Act
        var result = CorpusGenerator.Convert(request, input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert.That(result.ExitCode).IsNull();
            await Assert.That(result.Diagnostics.Count).IsEqualTo(1);
            await Assert
                .That(result.Diagnostics[0].ExceptionType)
                .IsEqualTo(typeof(Win32Exception).FullName);
            await Assert.That(result.Diagnostics[0].Message).Contains("変換器を起動できません");
        }
    }
}
