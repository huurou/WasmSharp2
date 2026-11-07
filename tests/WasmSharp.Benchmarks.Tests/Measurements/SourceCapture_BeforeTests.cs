using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class SourceCapture_BeforeTests
{
    [Test]
    public async Task 生成先と不存在のファイルを明示参照する_存在する生成先外の使用ファイルだけを採取する()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        fixture.Write("src/WasmSharp/bin/Generated.cs", "class BinaryGenerated {}");
        fixture.Write("src/WasmSharp/obj/Generated.cs", "class IntermediateGenerated {}");
        fixture.Write("src/WasmSharp/Included.txt", "used additional file");
        var projectPath = Path.Combine(
            fixture.RepositoryDirectory,
            "src/WasmSharp/WasmSharp.csproj"
        );
        File.WriteAllText(
            projectPath,
            File.ReadAllText(projectPath)
                .Replace(
                    "</Project>",
                    "<ItemGroup><AdditionalFiles Include=\"bin/Generated.cs;obj/Generated.cs;Missing.cs;Included.txt\" /></ItemGroup></Project>"
                )
        );
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var capture = new SourceCapture(fixture.RepositoryDirectory);

        // Act
        var source = capture.Before(store, []);

        // Assert
        var paths = source.Runtime.Files.Select(x => x.RelativePath).ToList();
        using (Assert.Multiple())
        {
            await Assert.That(paths).Contains("src/WasmSharp/Included.txt");
            await Assert.That(paths.Contains("src/WasmSharp/bin/Generated.cs")).IsFalse();
            await Assert.That(paths.Contains("src/WasmSharp/obj/Generated.cs")).IsFalse();
            await Assert.That(paths.Contains("src/WasmSharp/Missing.cs")).IsFalse();
        }
    }

    [Test]
    public async Task 必須projectが存在しない_対象pathを含むファイル不在を返す()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        var path = Path.Combine(fixture.RepositoryDirectory, "src/WasmSharp/WasmSharp.csproj");
        File.Delete(path);
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var capture = new SourceCapture(fixture.RepositoryDirectory);

        // Act & Assert
        var exception = await Assert
            .That(() => capture.Before(store, []))
            .ThrowsExactly<FileNotFoundException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.FileName).IsEqualTo(path);
            await Assert.That(exception.Message).Contains("使用projectが存在しません。");
        }
    }

    [Test]
    public async Task 共通ビルド設定と依存設定を置く_runtimeとharnessの両方の使用ファイルへ含める()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        string[] settings =
        [
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
        ];
        foreach (var setting in settings)
        {
            fixture.Write(setting, "<Project />");
        }
        fixture.Write("global.json", "{}");
        fixture.Write("NuGet.Config", "<configuration />");
        fixture.Write(".editorconfig", "root = true\n");
        fixture.Write("packages.lock.json", "{}");
        var expected = settings
            .Concat(["global.json", "NuGet.Config", ".editorconfig", "packages.lock.json"])
            .ToList();
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var capture = new SourceCapture(fixture.RepositoryDirectory);

        // Act
        var source = capture.Before(store, []);

        // Assert
        using (Assert.Multiple())
        {
            foreach (var setting in expected)
            {
                await Assert
                    .That(source.Runtime.Files.Select(x => x.RelativePath))
                    .Contains(setting);
                await Assert
                    .That(source.Harness.Files.Select(x => x.RelativePath))
                    .Contains(setting);
                await Assert
                    .That(source.UntrackedFiles.Select(x => x.RelativePath))
                    .Contains(setting);
            }
        }
    }

    [Test]
    public async Task リポジトリ外のsourceを参照する_対象pathを示して拒否する()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var externalPath = Path.Combine(fixture.Run.DirectoryPath, "External.cs");
        File.WriteAllText(externalPath, "class External {}");
        fixture.Write(
            "src/WasmSharp/WasmSharp.csproj",
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"{externalPath}\" /></ItemGroup></Project>"
        );
        var capture = new SourceCapture(fixture.RepositoryDirectory);

        // Act & Assert
        var exception = await Assert
            .That(() => capture.Before(store, []))
            .ThrowsExactly<InvalidOperationException>();
        await Assert
            .That(exception!.Message)
            .IsEqualTo("使用project/sourceがリポジトリ外にあります: " + externalPath);
    }

    [Test]
    [Arguments("clean", false)]
    [Arguments("untracked", true)]
    [Arguments("uninitialized", false)]
    public async Task Submoduleの状態が異なる_revisionと未コミット変更を記録する(
        string state,
        bool dirty
    )
    {
        // Arrange
        using var dependency = new SourceCaptureFixture();
        using var fixture = new SourceCaptureFixture();
        const string RELATIVE_PATH = "thirdParties/dependency";
        fixture.Git(
            "-c",
            "protocol.file.allow=always",
            "submodule",
            "add",
            "--",
            dependency.RepositoryDirectory,
            RELATIVE_PATH
        );
        if (state == "untracked")
        {
            fixture.Write(RELATIVE_PATH + "/Untracked.cs", "class Untracked {}");
        }
        else if (state == "uninitialized")
        {
            fixture.Git("submodule", "deinit", "--force", "--", RELATIVE_PATH);
        }
        var revision = dependency.Git("rev-parse", "HEAD");
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var capture = new SourceCapture(fixture.RepositoryDirectory);

        // Act
        var source = capture.Before(store, []);

        // Assert
        var submodule = source.Submodules.Single();
        using (Assert.Multiple())
        {
            await Assert.That(submodule.RelativePath).IsEqualTo(RELATIVE_PATH);
            await Assert.That(submodule.Revision).IsEqualTo(revision);
            await Assert.That(submodule.IsDirty).IsEqualTo(dirty);
        }
    }
}
