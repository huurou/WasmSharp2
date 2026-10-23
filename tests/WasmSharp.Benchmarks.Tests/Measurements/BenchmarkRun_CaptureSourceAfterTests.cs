using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_CaptureSourceAfterTests
{
    [Test]
    public async Task 使用ファイルを追加変更削除する_sourceとprojectとassemblyの途中変更を記録する()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        var assembly = fixture.Write("output/runtime.dll", "runtime assembly");
        var deletedAssembly = fixture.Write("output/harness.dll", "harness assembly");
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var before = BenchmarkRun.CaptureSourceBefore(
            fixture.RepositoryDirectory,
            store,
            [(AssemblyRole.Runtime, assembly), (AssemblyRole.Harness, deletedAssembly)]
        );
        fixture.Write("src/WasmSharp/Runtime.cs", "class Runtime { int changed; }\n");
        fixture.Write("tools/WasmSharp.Benchmarks/New.cs", "class New {}\n");
        File.Delete(
            Path.Combine(fixture.RepositoryDirectory, "src/WasmSharp.Generators/Generator.cs")
        );
        File.Delete(
            Path.Combine(
                fixture.RepositoryDirectory,
                "src/WasmSharp.Generators/WasmSharp.Generators.csproj"
            )
        );
        var project = Path.Combine(
            fixture.RepositoryDirectory,
            "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj"
        );
        File.WriteAllText(
            project,
            File.ReadAllText(project)
                .Replace(
                    "</Project>",
                    "<ItemGroup><ProjectReference Include=\"Extra/Extra.csproj\" /></ItemGroup></Project>"
                )
        );
        fixture.Write(
            "tools/WasmSharp.Benchmarks/Extra/Extra.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
        );
        fixture.Write("output/runtime.dll", "changed assembly");
        File.Delete(deletedAssembly);
        fixture.Write("unrelated.cs", "unrelated");

        // Act
        var after = BenchmarkRun.CaptureSourceAfter(fixture.RepositoryDirectory, store, before);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(after.Changes.Single(x => x.Path == "src/WasmSharp/Runtime.cs").Kind)
                .IsEqualTo(ContentKind.Source);
            await Assert
                .That(
                    after
                        .Changes.Single(x => x.Path == "tools/WasmSharp.Benchmarks/New.cs")
                        .BeforeSha256
                )
                .IsNull();
            await Assert
                .That(
                    after
                        .Changes.Single(x => x.Path == "src/WasmSharp.Generators/Generator.cs")
                        .AfterSha256
                )
                .IsNull();
            await Assert
                .That(
                    after
                        .Changes.Single(x =>
                            x.Path == "src/WasmSharp.Generators/WasmSharp.Generators.csproj"
                        )
                        .Kind
                )
                .IsEqualTo(ContentKind.Project);
            await Assert
                .That(
                    after
                        .Changes.Single(x =>
                            x.Path == "src/WasmSharp.Generators/WasmSharp.Generators.csproj"
                        )
                        .AfterSha256
                )
                .IsNull();
            await Assert
                .That(
                    after
                        .Changes.Single(x =>
                            x.Path == "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj"
                        )
                        .Kind
                )
                .IsEqualTo(ContentKind.Project);
            await Assert
                .That(
                    after
                        .Changes.Single(x =>
                            x.Path == "tools/WasmSharp.Benchmarks/Extra/Extra.csproj"
                        )
                        .BeforeSha256
                )
                .IsNull();
            await Assert
                .That(after.Changes.Single(x => x.Path == assembly).Kind)
                .IsEqualTo(ContentKind.Assembly);
            await Assert
                .That(after.Changes.Single(x => x.Path == deletedAssembly).AfterSha256)
                .IsNull();
            await Assert.That(after.Changes.Any(x => x.Path == "unrelated.cs")).IsFalse();
            await Assert
                .That(after.AssembliesAfter.Single().Sha256)
                .IsNotEqualTo(before.AssembliesBefore.First().Sha256);
            await Assert.That(after.Runtime.Sha256).IsEqualTo(before.Runtime.Sha256);
        }
    }

    [Test]
    public async Task runtimeだけを変更する_harness集合のhashは変わらず無変更の実行後照合も成立する()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        using var nextRun = new RunStoreFixture();
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);
        var first = BenchmarkRun.CaptureSourceBefore(fixture.RepositoryDirectory, store, []);
        var unchanged = BenchmarkRun.CaptureSourceAfter(fixture.RepositoryDirectory, store, first);
        fixture.Write("src/WasmSharp/Runtime.cs", "class Runtime { int changed; }\n");
        var nextStore = RunStore.Create(nextRun.DirectoryPath, nextRun.Manifest);

        // Act
        var second = BenchmarkRun.CaptureSourceBefore(fixture.RepositoryDirectory, nextStore, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(unchanged.Changes).IsEmpty();
            await Assert.That(second.Runtime.Sha256).IsNotEqualTo(first.Runtime.Sha256);
            await Assert.That(second.Harness.Sha256).IsEqualTo(first.Harness.Sha256);
        }
    }
}
