using System.Security.Cryptography;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal sealed class Core3ConversionFixture(TemporaryDirectory directory, RunnerCli cli)
{
    internal static async Task<Core3ConversionFixture> CreateAsync(TemporaryDirectory directory, string name, string source)
    {
        var inputPath = directory.Combine($"spec/test/core/{name}");
        Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
        await File.WriteAllTextAsync(inputPath, source);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(inputPath)));
        var loader = new ConversionProfileLoader();
        var cli = new RunnerCli(loader.Load(), () => loader.LoadCore3() with { Inputs = [new SourceInput(name, sha256)] });
        return new Core3ConversionFixture(directory, cli);
    }

    internal (int ExitCode, string Error) Generate(string output)
    {
        return Run([
            "generate",
            "--spec-root",
            directory.Combine("spec"),
            "--profile",
            "core3",
            "--wasm-tools",
            SuiteWorkspace.ConverterPath,
            "--output",
            directory.Combine(output),
        ]);
    }

    internal (int ExitCode, string Error) Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = cli.Run(args, output, error);
        return (exitCode, error.ToString());
    }
}
