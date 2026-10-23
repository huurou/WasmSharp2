using System.Diagnostics;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class SourceCaptureFixture : IDisposable
{
    internal string RepositoryDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "wasm-source-capture-" + Guid.NewGuid().ToString("N"));

    internal RunStoreFixture Run { get; } = new();

    internal SourceCaptureFixture()
    {
        Write(
            "src/WasmSharp/WasmSharp.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="../WasmSharp.Generators/WasmSharp.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" /></ItemGroup>
            </Project>
            """
        );
        Write("src/WasmSharp/Runtime.cs", "class Runtime {}\n");
        Write(
            "src/WasmSharp.Generators/WasmSharp.Generators.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><AdditionalFiles Include="Releases.md" /></ItemGroup>
            </Project>
            """
        );
        Write("src/WasmSharp.Generators/Generator.cs", "class Generator {}\n");
        Write("src/WasmSharp.Generators/Releases.md", "generator dependency\n");
        Write(
            "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="../../src/WasmSharp/WasmSharp.csproj" /></ItemGroup>
            </Project>
            """
        );
        Write("tools/WasmSharp.Benchmarks/Harness.cs", "class Harness {}\n");
        Write(".gitignore", "**/bin/\n**/obj/\n");
        Git("init", "--quiet");
        Git("add", ".");
        Git(
            "-c",
            "user.name=Fixture",
            "-c",
            "user.email=fixture@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "初期素材"
        );
    }

    internal string Write(string relativePath, string content)
    {
        var path = Path.GetFullPath(Path.Combine(RepositoryDirectory, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    internal string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error);
        }

        return output.Trim();
    }

    public void Dispose()
    {
        Run.Dispose();
        foreach (
            var path in Directory.EnumerateFiles(
                RepositoryDirectory,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(RepositoryDirectory, recursive: true);
    }
}
