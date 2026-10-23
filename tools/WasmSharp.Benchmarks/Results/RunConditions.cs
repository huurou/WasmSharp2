using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// run固有情報を取り除き、意味のある値を条件名へ対応付ける
/// </summary>
internal sealed class RunConditions(string runId)
{
    internal string RunId { get; } = runId;
    internal Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    internal static RunConditions Create(
        RunManifest run,
        BenchmarkCase definition,
        AssetManifest assets,
        RunEnvironment environment,
        RunSource source,
        List<ProcessConfiguration> jobs
    )
    {
        var conditions = new RunConditions(run.RunId);
        var paths = new ConditionPaths(environment.Command, source, assets);
        conditions.Add("run.schema_version", run.SchemaVersion);
        conditions.Add(
            "run.planned_cases",
            run.PlannedCaseIds.Order(StringComparer.Ordinal).ToArray()
        );
        conditions.Add(
            "case",
            definition with
            {
                Workload = definition
                    .Workload.OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ToList(),
            }
        );
        conditions.Add("assets.schema_version", assets.SchemaVersion);
        conditions.Add("assets.profile.sha256", assets.Profile.Sha256);
        var asset = assets.Assets.Single(x => x.AssetId == definition.AssetId);
        conditions.Add(
            "asset.binary",
            new
            {
                Path = "binary:" + paths.AssetPath(asset.Binary.RelativePath),
                asset.Binary.Sha256,
            }
        );
        conditions.Add(
            "asset.sources",
            asset.Sources.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray()
        );
        for (var index = 0; index < asset.GenerationSteps.Count; index++)
        {
            var step = asset.GenerationSteps[index];
            conditions.Add(
                $"asset.generation[{index}].tool",
                new
                {
                    step.Tool.Name,
                    step.Tool.Version,
                    step.Tool.Revision,
                    step.Tool.Executable.Sha256,
                    step.Tool.BuildConfiguration,
                }
            );
            conditions.Add(
                $"asset.generation[{index}].arguments",
                step.Arguments.Select(paths.AssetArgument).ToArray()
            );
        }

        // runtime集合に含まれるproject・共通設定もharnessのビルド条件を変えうる
        foreach (
            var file in source
                .Harness.Files.Concat(
                    source.Runtime.Files.Where(x =>
                        !x.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    )
                )
                .DistinctBy(x => x.RelativePath)
                .OrderBy(x => x.RelativePath, StringComparer.Ordinal)
        )
        {
            conditions.Add(
                "source.files[" + file.RelativePath.Replace('\\', '/') + "].sha256",
                file.Sha256
            );
        }
        conditions.Add(
            "source.submodules",
            source.Submodules.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray()
        );
        conditions.Add(
            "environment",
            new
            {
                environment.Cpu,
                environment.LogicalProcessorCount,
                environment.OperatingSystem,
                environment.OsArchitecture,
                environment.ProcessArchitecture,
                environment.SdkVersion,
                environment.RuntimeVersion,
                environment.BenchmarkDotNetVersion,
                environment.BuildConfiguration,
                environment.Power,
            }
        );
        conditions.Add("environment.gc", Settings(environment.GcSettings, paths));
        conditions.Add("environment.jit", Settings(environment.JitSettings, paths));
        conditions.Add("environment.variables", Settings(environment.EnvironmentVariables, paths));
        conditions.Add("environment.processes", Processes(environment.Processes, paths));
        conditions.Add("job.processes", Processes(jobs, paths));
        conditions.Add("command.executable", paths.CommandExecutable());
        conditions.Add("command.arguments", paths.CommandArguments());
        return conditions;
    }

    private static SortedDictionary<string, string> Settings(
        IEnumerable<NamedSetting> settings,
        ConditionPaths paths
    ) =>
        new(
            settings
                .Where(x =>
                    x.Name
                        is not "WASMSHARP_BENCHMARK_RUN_CONFIG"
                            and not "RuntimeConfigFile.RelativePath"
                )
                .ToDictionary(
                    x => x.Name,
                    x =>
                        x.Name is "DOTNET_ROOT" or "DOTNET_ROOT_X64"
                            ? "tool-root:dotnet"
                            : paths.Value(x.Value)
                ),
            StringComparer.Ordinal
        );

    private static object[] Processes(
        IEnumerable<ProcessConfiguration> processes,
        ConditionPaths paths
    ) =>
        processes
            .Select(x => new
            {
                x.Role,
                x.LaunchCount,
                JobSettings = Settings(x.JobSettings, paths),
                RuntimeConfiguration = Settings(x.RuntimeConfiguration, paths),
                EnvironmentVariables = Settings(x.EnvironmentVariables, paths),
            })
            .OrderBy(x => JsonSerializer.Serialize(x, CaseJson.Options), StringComparer.Ordinal)
            .Cast<object>()
            .ToArray();

    private void Add<T>(string name, T value) =>
        Flatten(name, JsonSerializer.SerializeToElement(value, CaseJson.Options));

    private void Flatten(string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any())
        {
            foreach (var property in value.EnumerateObject())
            {
                Flatten(name + "." + property.Name, property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() != 0)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                Flatten(name + "[" + index++ + "]", item);
            }
        }
        else
        {
            Values.Add(
                name,
                value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText()
            );
        }
    }
}

/// <summary>
/// 保存されたpathの役割を照合用の表現へ移し、未知の引数は保持する
/// </summary>
internal sealed class ConditionPaths
{
    private readonly CommandInvocation command_;
    private readonly RunSource source_;
    private readonly AssetManifest assets_;
    private readonly string root_;

    internal ConditionPaths(CommandInvocation command, RunSource source, AssetManifest assets)
    {
        command_ = command;
        source_ = source;
        assets_ = assets;
        root_ = Normalize(command.WorkingDirectory).TrimEnd('/');
        // 保存cwdを基点にprojectを解決し、source集合の相対pathからリポジトリ基準を復元する
        foreach (var argument in command.Arguments)
        {
            var normalized = ProjectPath(argument.Split('=', 2)[^1]);
            var project = source.Harness.Files.FirstOrDefault(x =>
                x.RelativePath.EndsWith(".csproj", StringComparison.Ordinal)
                && normalized.EndsWith("/" + x.RelativePath, StringComparison.OrdinalIgnoreCase)
            );
            if (project is not null)
            {
                root_ = normalized[..^(project.RelativePath.Length + 1)];
                break;
            }
        }
    }

    internal string CommandExecutable()
    {
        var name = FileName(command_.ExecutablePath);
        if (name is "dotnet" or "dotnet.exe")
        {
            return "tool:dotnet";
        }
        var harness = source_.AssembliesBefore.FirstOrDefault(x => x.Role == AssemblyRole.Harness);
        if (
            harness is not null
            && Path.GetExtension(name) is "" or ".exe" or ".dll"
            && Path.GetFileNameWithoutExtension(name)
                == Path.GetFileNameWithoutExtension(FileName(harness.Path))
        )
        {
            return "assembly:Harness";
        }
        return Value(command_.ExecutablePath);
    }

    internal string[] CommandArguments()
    {
        var values = new List<string>();
        for (var index = 0; index < command_.Arguments.Count; index++)
        {
            var argument = command_.Arguments[index];
            var parts = argument.Split('=', 2);
            if (parts[0] is "--output" or "--artifacts" or "--run-id" or "--run-config")
            {
                if (parts.Length == 1)
                {
                    index++;
                }
                continue;
            }
            if (parts[0] is "--asset-manifest" or "--assets")
            {
                values.Add("--asset-manifest=asset-manifest");
                if (parts.Length == 1)
                {
                    index++;
                }
                continue;
            }
            values.Add(parts.Length == 2 ? parts[0] + "=" + Value(parts[1]) : Value(argument));
        }
        return values.ToArray();
    }

    internal string Value(string value)
    {
        var path = ProjectPath(value);
        if (path.Equals(root_, StringComparison.OrdinalIgnoreCase))
        {
            return "repository-root";
        }
        var assembly = source_.AssembliesBefore.FirstOrDefault(x =>
            Normalize(x.Path).Equals(path, StringComparison.OrdinalIgnoreCase)
        );
        if (assembly is not null)
        {
            return "assembly:" + assembly.Role;
        }
        if (path.StartsWith(root_ + "/", StringComparison.OrdinalIgnoreCase))
        {
            var relative = path[(root_.Length + 1)..];
            return (
                    relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                        ? "project:"
                        : "repository:"
                ) + relative;
        }
        return path;
    }

    internal string AssetPath(string value) => Normalize(value);

    internal string AssetArgument(string value)
    {
        var path = Normalize(value);
        var references = assets_
            .Assets.SelectMany(x =>
                x.Sources.Select(y => y.RelativePath).Append(x.Binary.RelativePath)
            )
            .Append(assets_.Profile.RelativePath);
        foreach (var reference in references)
        {
            var relative = Normalize(reference);
            if (
                path == relative
                || path.EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase)
            )
            {
                return "asset:" + relative;
            }
        }
        return Value(value);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private string ProjectPath(string path) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? Normalize(Path.GetFullPath(path, command_.WorkingDirectory))
            : Normalize(path);

    private static string FileName(string path) => Normalize(path).Split('/')[^1];
}
