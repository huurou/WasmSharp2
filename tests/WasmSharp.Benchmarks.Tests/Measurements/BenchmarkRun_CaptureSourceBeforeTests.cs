using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_CaptureSourceBeforeTests
{
    [Test]
    public async Task Gitの接頭辞を省略する_保存したindexとworktreeの差分を順に復元できる()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        fixture.Git("config", "--local", "diff.noprefix", "true");
        const string RELATIVE_PATH = "src/WasmSharp/Runtime.cs";
        const string INDEX_CONTENT = "class Runtime { int staged; }\n";
        const string WORKTREE_CONTENT = "class Runtime { int staged; int worktree; }\n";
        var path = fixture.Write(RELATIVE_PATH, INDEX_CONTENT);
        fixture.Git("add", RELATIVE_PATH);
        fixture.Write(RELATIVE_PATH, WORKTREE_CONTENT);
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);

        // Act
        var source = BenchmarkRun.CaptureSourceBefore(fixture.RepositoryDirectory, store, []);
        fixture.Git("restore", "--source=HEAD", "--staged", "--worktree", "--", RELATIVE_PATH);
        fixture.Git(
            "apply",
            "--index",
            Path.Combine(fixture.Run.DirectoryPath, source.IndexDiff.RelativePath)
        );
        var restoredIndex = File.ReadAllText(path);
        fixture.Git(
            "apply",
            Path.Combine(fixture.Run.DirectoryPath, source.WorktreeDiff.RelativePath)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(restoredIndex).IsEqualTo(INDEX_CONTENT);
            await Assert.That(File.ReadAllText(path)).IsEqualTo(WORKTREE_CONTENT);
            await Assert
                .That(fixture.Git("show", ":" + RELATIVE_PATH))
                .IsEqualTo(INDEX_CONTENT.Trim());
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Gitの色表示を強制する_保存した差分からindexとworktreeを復元できる(
        bool indexDiff
    )
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        fixture.Git("config", "--local", "color.ui", "always");
        const string RELATIVE_PATH = "src/WasmSharp/Runtime.cs";
        const string INDEX_CONTENT = "class Runtime { int staged; }\n";
        const string WORKTREE_CONTENT = "class Runtime { int staged; int worktree; }\n";
        var path = fixture.Write(RELATIVE_PATH, INDEX_CONTENT);
        fixture.Git("add", RELATIVE_PATH);
        fixture.Write(RELATIVE_PATH, WORKTREE_CONTENT);
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);

        // Act
        var source = BenchmarkRun.CaptureSourceBefore(fixture.RepositoryDirectory, store, []);
        var patch = Path.Combine(
            fixture.Run.DirectoryPath,
            indexDiff ? source.IndexDiff.RelativePath : source.WorktreeDiff.RelativePath
        );
        if (indexDiff)
        {
            fixture.Git("restore", "--source=HEAD", "--staged", "--worktree", "--", RELATIVE_PATH);
            fixture.Git("apply", "--index", patch);
        }
        else
        {
            fixture.Git("restore", "--worktree", "--", RELATIVE_PATH);
            fixture.Git("apply", patch);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(File.ReadAllText(path))
                .IsEqualTo(indexDiff ? INDEX_CONTENT : WORKTREE_CONTENT);
            await Assert
                .That(fixture.Git("show", ":" + RELATIVE_PATH))
                .IsEqualTo(INDEX_CONTENT.Trim());
        }
    }

    [Test]
    public async Task 使用sourceを採取する_生成器をruntimeへ分離し使用未追跡ファイルと差分だけを保存する()
    {
        // Arrange
        using var fixture = new SourceCaptureFixture();
        fixture.Write("src/WasmSharp.Generators/Generator.cs", "class Generator { int staged; }\n");
        fixture.Git("add", "src/WasmSharp.Generators/Generator.cs");
        fixture.Write("src/WasmSharp/Runtime.cs", "class Runtime { int worktree; }\n");
        fixture.Write("tools/WasmSharp.Benchmarks/New.cs", "class New {}\n");
        fixture.Write("tools/WasmSharp.Benchmarks/obj/Ignored.cs", "ignored");
        fixture.Write("tools/WasmSharp.Benchmarks/notes.txt", "private unrelated notes");
        fixture.Write("unrelated.cs", "unrelated source");
        var assembly = fixture.Write("output/runtime.dll", "runtime assembly");
        var store = RunStore.Create(fixture.Run.DirectoryPath, fixture.Run.Manifest);

        // Act
        var source = BenchmarkRun.CaptureSourceBefore(
            fixture.RepositoryDirectory,
            store,
            [(AssemblyRole.Runtime, assembly)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(source.Head).IsEqualTo(fixture.Git("rev-parse", "HEAD"));
            await Assert
                .That(source.Runtime.Files.Select(x => x.RelativePath))
                .Contains("src/WasmSharp.Generators/Generator.cs");
            await Assert
                .That(source.Runtime.Files.Select(x => x.RelativePath))
                .Contains("src/WasmSharp.Generators/Releases.md");
            await Assert
                .That(
                    source.Harness.Files.Select(x => x.RelativePath).Any(x => x.StartsWith("src/"))
                )
                .IsFalse();
            await Assert
                .That(source.UntrackedFiles.Select(x => x.RelativePath))
                .IsEquivalentTo(["tools/WasmSharp.Benchmarks/New.cs"]);
            await Assert
                .That(
                    File.ReadAllText(
                        Path.Combine(fixture.Run.DirectoryPath, source.IndexDiff.RelativePath)
                    )
                )
                .Contains("staged");
            await Assert
                .That(
                    File.ReadAllText(
                        Path.Combine(fixture.Run.DirectoryPath, source.WorktreeDiff.RelativePath)
                    )
                )
                .Contains("worktree");
            await Assert
                .That(
                    File.ReadAllText(
                        Path.Combine(
                            fixture.Run.DirectoryPath,
                            source.UntrackedFiles.Single().RelativePath
                        )
                    )
                )
                .IsEqualTo("class New {}\n");
            await Assert.That(source.AssembliesBefore.Single().Path).IsEqualTo(assembly);
            await Assert
                .That(source.AssembliesBefore.Single().Sha256)
                .IsEqualTo("6a2027a10880d5f8b06d20e1129d08a16ada440d387c8408cd6daace20c0aee4");
        }
    }
}
