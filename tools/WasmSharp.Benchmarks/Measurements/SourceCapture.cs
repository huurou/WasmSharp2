using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 使用projectと評価済みsourceの集合、Gitの復元情報を採取する
/// </summary>
internal sealed class SourceCapture(string repositoryDirectory)
{
    private readonly string repositoryDirectory_ = Path.GetFullPath(repositoryDirectory);

    internal RunSource Before(
        RunStore store,
        IEnumerable<(AssemblyRole Role, string Path)> assemblies
    )
    {
        var head = Git("rev-parse", "HEAD").Trim();
        var (runtime, harness) = Sources(head);
        var used = runtime.Files.Concat(harness.Files).Select(x => x.RelativePath).ToHashSet();
        var untracked = Git("ls-files", "--others", "--exclude-standard", "-z")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => used.Contains(x))
            .Order(StringComparer.Ordinal)
            .Select(x => store.SaveFile(x, File.ReadAllBytes(FullPath(x))))
            .ToList();

        return new(
            head,
            store.SaveFile(
                "source/index.diff",
                Encoding.UTF8.GetBytes(
                    Git(
                        "diff",
                        "--no-color",
                        "--src-prefix=a/",
                        "--dst-prefix=b/",
                        "--cached",
                        "--binary",
                        "--full-index",
                        "--no-ext-diff",
                        "--no-textconv"
                    )
                )
            ),
            store.SaveFile(
                "source/worktree.diff",
                Encoding.UTF8.GetBytes(
                    Git(
                        "diff",
                        "--no-color",
                        "--src-prefix=a/",
                        "--dst-prefix=b/",
                        "--binary",
                        "--full-index",
                        "--no-ext-diff",
                        "--no-textconv"
                    )
                )
            ),
            untracked,
            Submodules(),
            runtime,
            harness,
            assemblies
                .Select(x => new AssemblyIdentity(x.Role, Path.GetFullPath(x.Path), Hash(x.Path)))
                .ToList(),
            [],
            []
        );
    }

    internal RunSource After(RunSource before)
    {
        var (runtime, harness) = Sources(before.Head, allowMissing: true);
        var previous = before
            .Runtime.Files.Concat(before.Harness.Files)
            .DistinctBy(x => x.RelativePath)
            .ToDictionary(x => x.RelativePath, x => x.Sha256);
        var current = runtime
            .Files.Concat(harness.Files)
            .DistinctBy(x => x.RelativePath)
            .ToDictionary(x => x.RelativePath, x => x.Sha256);
        // 実行前に使用したファイルは、projectから外れても存在する限り内容を照合する
        foreach (
            var path in previous.Keys.Where(x =>
                !current.ContainsKey(x) && File.Exists(FullPath(x))
            )
        )
        {
            current.Add(path, Hash(FullPath(path)));
        }
        var changes = previous
            .Keys.Union(current.Keys)
            .Order(StringComparer.Ordinal)
            .Select(x => new ContentChange(
                IsProject(x) ? ContentKind.Project : ContentKind.Source,
                x,
                previous.GetValueOrDefault(x),
                current.GetValueOrDefault(x)
            ))
            .Where(x => x.BeforeSha256 != x.AfterSha256)
            .ToList();
        var assemblies = new List<AssemblyIdentity>();
        foreach (var assembly in before.AssembliesBefore)
        {
            var hash = File.Exists(assembly.Path) ? Hash(assembly.Path) : null;
            if (hash is not null)
            {
                assemblies.Add(assembly with { Sha256 = hash });
            }

            if (hash != assembly.Sha256)
            {
                changes.Add(new(ContentKind.Assembly, assembly.Path, assembly.Sha256, hash));
            }
        }

        return before with
        {
            AssembliesAfter = assemblies,
            Changes = changes,
        };
    }

    private static bool IsProject(string path) =>
        Path.GetExtension(path) is ".csproj" or ".props" or ".targets"
        || Path.GetFileName(path)
            is "global.json"
                or "NuGet.Config"
                or "nuget.config"
                or "packages.lock.json";

    private (SourceSet Runtime, SourceSet Harness) Sources(string head, bool allowMissing = false)
    {
        var runtime = new HashSet<string>(StringComparer.Ordinal);
        AddProject("src/WasmSharp/WasmSharp.csproj", runtime, [], allowMissing);
        var harness = new HashSet<string>(StringComparer.Ordinal);
        AddProject(
            "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj",
            harness,
            runtime,
            allowMissing
        );
        return (Set(head, runtime), Set(head, harness));
    }

    private void AddProject(
        string relativePath,
        HashSet<string> files,
        HashSet<string> excluded,
        bool allowMissing
    )
    {
        if (excluded.Contains(relativePath))
        {
            return;
        }

        if (!File.Exists(FullPath(relativePath)))
        {
            if (allowMissing)
            {
                return;
            }

            throw new FileNotFoundException("使用projectが存在しません。", FullPath(relativePath));
        }

        if (!files.Add(relativePath))
        {
            return;
        }

        var projectPath = FullPath(relativePath);
        using var evaluation = JsonDocument.Parse(
            CaptureCommand.Run(
                "dotnet",
                repositoryDirectory_,
                "msbuild",
                projectPath,
                "-nologo",
                "-property:Configuration=Release",
                "-getItem:Compile,AdditionalFiles,EmbeddedResource,ProjectReference,EditorConfigFiles"
            )
        );
        var items = evaluation.RootElement.GetProperty("Items");
        foreach (
            var kind in new[]
            {
                "Compile",
                "AdditionalFiles",
                "EmbeddedResource",
                "EditorConfigFiles",
            }
        )
        {
            foreach (var item in items.GetProperty(kind).EnumerateArray())
            {
                AddFile(item.GetProperty("FullPath").GetString()!, files);
            }
        }

        foreach (var item in items.GetProperty("ProjectReference").EnumerateArray())
        {
            AddProject(
                RelativePath(item.GetProperty("FullPath").GetString()!),
                files,
                excluded,
                allowMissing
            );
        }

        for (
            var directory = Path.GetDirectoryName(projectPath)!;
            ;
            directory = Path.GetDirectoryName(directory)!
        )
        {
            foreach (
                var name in new[]
                {
                    "Directory.Build.props",
                    "Directory.Build.targets",
                    "Directory.Packages.props",
                    "global.json",
                    "NuGet.Config",
                    "nuget.config",
                    ".editorconfig",
                    "packages.lock.json",
                }
            )
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path))
                {
                    AddFile(path, files);
                }
            }

            if (directory == repositoryDirectory_)
            {
                break;
            }
        }
    }

    private void AddFile(string path, HashSet<string> files)
    {
        var relativePath = RelativePath(path);
        if (File.Exists(path) && !relativePath.Split('/').Any(x => x is "bin" or "obj"))
        {
            files.Add(relativePath);
        }
    }

    private SourceSet Set(string head, HashSet<string> paths)
    {
        var files = paths
            .Order(StringComparer.Ordinal)
            .Select(x => new ManifestFile(x, Hash(FullPath(x))))
            .ToList();
        // pathと内容hashを別々のJSON要素にし、並びと区切りを固定する
        var identity = JsonSerializer.SerializeToUtf8Bytes(files, CaseJson.Options);
        return new(head, Convert.ToHexStringLower(SHA256.HashData(identity)), files);
    }

    private List<SubmoduleRevision> Submodules() =>
        Git("submodule", "status", "--recursive")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x =>
            {
                var parts = x[1..].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                var path = parts[1];
                var initialized = x[0] != '-';
                var dirty =
                    x[0] is '+' or 'U'
                    || (
                        initialized
                        && CaptureCommand
                            .Run(
                                "git",
                                FullPath(path),
                                "status",
                                "--porcelain",
                                "--untracked-files=normal"
                            )
                            .Length != 0
                    );
                return new SubmoduleRevision(path, parts[0], dirty);
            })
            .ToList();

    private string RelativePath(string path)
    {
        var relativePath = Path.GetRelativePath(repositoryDirectory_, Path.GetFullPath(path))
            .Replace('\\', '/');
        if (
            relativePath == ".."
            || relativePath.StartsWith("../", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
        )
        {
            throw new InvalidOperationException(
                "使用project/sourceがリポジトリ外にあります: " + path
            );
        }

        return relativePath;
    }

    private string FullPath(string path) => Path.Combine(repositoryDirectory_, path);

    private string Git(params string[] arguments) =>
        CaptureCommand.Run("git", repositoryDirectory_, arguments);

    private static string Hash(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
