using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// run固有情報を取り除き、意味のある値を条件名へ対応付ける
/// </summary>
internal sealed class RunConditions(string runId, CaseJson json)
{
    private readonly CaseJson _json = json;

    internal string RunId { get; } = runId;
    internal Dictionary<string, string> Values { get; } = [with(StringComparer.Ordinal)];

    internal static RunConditions Create(
        RunManifest run,
        BenchmarkCase definition,
        AssetManifest assets,
        RunEnvironment environment,
        RunSource source,
        List<ProcessConfiguration> jobs,
        List<ProcessCheck> checks,
        CaseJson json
    )
    {
        var conditions = new RunConditions(run.RunId, json);
        var paths = new ConditionPaths(environment.Command, source, assets);
        conditions.Add("run.schema_version", run.SchemaVersion);
        conditions.Add("run.planned_cases", run.PlannedCaseIds.Order(StringComparer.Ordinal).ToArray());
        conditions.Add(
            "case",
            definition with
            {
                Workload = [.. definition.Workload.OrderBy(x => x.Name, StringComparer.Ordinal)],
            }
        );
        conditions.Add("assets.schema_version", assets.SchemaVersion);
        conditions.Add("assets.profile.sha256", assets.Profile.Sha256);
        var asset = assets.Assets.Single(x => x.AssetId == definition.AssetId);
        conditions.Add(
            "asset.binary",
            new { Path = "binary:" + ConditionPaths.AssetPath(asset.Binary.RelativePath), asset.Binary.Sha256 }
        );
        conditions.Add("asset.sources", asset.Sources.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray());
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
            conditions.Add($"asset.generation[{index}].arguments", step.Arguments.Select(paths.AssetArgument).ToArray());
        }

        // runtime集合に含まれるproject・共通設定もharnessのビルド条件を変えうる
        foreach (
            var file in source
                .Harness.Files.Concat(
                    source.Runtime.Files.Where(x => !x.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                )
                .DistinctBy(x => x.RelativePath)
        )
        {
            conditions.Add("source.files[" + file.RelativePath.Replace('\\', '/') + "].sha256", file.Sha256);
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
        conditions.Add("environment.processes", conditions.Processes(environment.Processes, paths));
        var jobConditions = jobs.Select(x => new
        {
            Configuration = Process(x, paths),
            Check = checks.First(y => y.JobId == x.JobId),
        })
            .ToArray();
        conditions.Add(
            "job.processes",
            jobConditions
                .Select(x => x.Configuration)
                .OrderBy(x => JsonSerializer.Serialize(x, json.Options), StringComparer.Ordinal)
                .ToArray()
        );
        conditions.Add(
            "process.affinity",
            jobConditions
                .Select(x => new { x.Configuration, Mask = x.Check.AffinityBefore })
                .OrderBy(x => JsonSerializer.Serialize(x, json.Options), StringComparer.Ordinal)
                .ToArray()
        );
        conditions.Add("command.executable", paths.CommandExecutable());
        conditions.Add(
            "process.runtime",
            jobConditions
                .Select(x => new { x.Configuration, Version = x.Check.RuntimeVersion })
                .OrderBy(x => JsonSerializer.Serialize(x, json.Options), StringComparer.Ordinal)
                .ToArray()
        );
        conditions.Add("command.arguments", paths.CommandArguments());
        return conditions;
    }

    private static SortedDictionary<string, string> Settings(IEnumerable<NamedSetting> settings, ConditionPaths paths)
    {
        return new(
            settings
                .Where(x => x.Name is not "WASMSHARP_BENCHMARK_RUN_CONFIG" and not "RuntimeConfigFile.RelativePath")
                .ToDictionary(
                    x => x.Name,
                    x => x.Name is "DOTNET_ROOT" or "DOTNET_ROOT_X64" ? "tool-root:dotnet" : paths.Value(x.Value)
                ),
            StringComparer.Ordinal
        );
    }

    private static object Process(ProcessConfiguration process, ConditionPaths paths)
    {
        return new
        {
            process.Role,
            process.LaunchCount,
            JobSettings = Settings(process.JobSettings, paths),
            RuntimeConfiguration = Settings(process.RuntimeConfiguration, paths),
            EnvironmentVariables = Settings(process.EnvironmentVariables, paths),
        };
    }

    private object[] Processes(IEnumerable<ProcessConfiguration> processes, ConditionPaths paths)
    {
        return
        [
            .. processes
                .Select(x => Process(x, paths))
                .OrderBy(x => JsonSerializer.Serialize(x, _json.Options), StringComparer.Ordinal),
        ];
    }

    private void Add<T>(string name, T value)
    {
        Flatten(name, JsonSerializer.SerializeToElement(value, _json.Options));
    }

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
            Values.Add(name, value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
        }
    }
}

/// <summary>
/// 保存されたpathの役割を照合用の表現へ移し、未知の引数は保持する
/// </summary>
internal sealed class ConditionPaths
{
    private readonly CommandInvocation _command;
    private readonly RunSource _source;
    private readonly AssetManifest _assets;
    private readonly string _root;

    internal ConditionPaths(CommandInvocation command, RunSource source, AssetManifest assets)
    {
        _command = command;
        _source = source;
        _assets = assets;
        _root = Normalize(command.WorkingDirectory).TrimEnd('/');
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
                _root = normalized[..^(project.RelativePath.Length + 1)];
                break;
            }
        }
    }

    internal string CommandExecutable()
    {
        var name = FileName(_command.ExecutablePath);
        if (name is "dotnet" or "dotnet.exe")
        {
            return "tool:dotnet";
        }
        var harness = _source.AssembliesBefore.FirstOrDefault(x => x.Role == AssemblyRole.Harness);
        if (harness is not null)
        {
            // 名前に含まれるドットと、実行形式の拡張子を区別する。
            var harnessName = Path.GetFileNameWithoutExtension(FileName(harness.Path));
            if (name == harnessName || name == $"{harnessName}.exe" || name == $"{harnessName}.dll")
            {
                return "assembly:Harness";
            }
        }
        return Value(_command.ExecutablePath);
    }

    internal string[] CommandArguments()
    {
        var values = new List<string>();
        for (var index = 0; index < _command.Arguments.Count; index++)
        {
            var argument = _command.Arguments[index];
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
        return [.. values];
    }

    internal string Value(string value)
    {
        var path = ProjectPath(value);
        if (path.Equals(_root, StringComparison.OrdinalIgnoreCase))
        {
            return "repository-root";
        }
        var assembly = _source.AssembliesBefore.FirstOrDefault(x =>
            Normalize(x.Path).Equals(path, StringComparison.OrdinalIgnoreCase)
        );
        if (assembly is not null)
        {
            return "assembly:" + assembly.Role;
        }
        if (path.StartsWith(_root + "/", StringComparison.OrdinalIgnoreCase))
        {
            var relative = path[(_root.Length + 1)..];
            return (relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? "project:" : "repository:")
                + relative;
        }
        return path;
    }

    internal static string AssetPath(string value)
    {
        return Normalize(value);
    }

    internal string AssetArgument(string value)
    {
        var path = Normalize(value);
        var references = _assets
            .Assets.SelectMany(x => x.Sources.Select(y => y.RelativePath).Append(x.Binary.RelativePath))
            .Append(_assets.Profile.RelativePath);
        foreach (var reference in references)
        {
            var relative = Normalize(reference);
            if (path == relative || path.EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase))
            {
                return "asset:" + relative;
            }
        }
        return Value(value);
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    private string ProjectPath(string path)
    {
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? Normalize(Path.GetFullPath(path, _command.WorkingDirectory))
            : Normalize(path);
    }

    private static string FileName(string path)
    {
        return Normalize(path).Split('/')[^1];
    }
}
