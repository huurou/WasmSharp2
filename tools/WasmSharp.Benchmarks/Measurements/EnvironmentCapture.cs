using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Microsoft.Win32;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 採取processの環境と指定された測定・診断構成を記録する
/// </summary>
internal sealed partial class EnvironmentCapture
{
    internal static string CurrentRuntimeVersion =>
        RuntimeInformation.FrameworkDescription + "; " + Version(typeof(object).Assembly);

    private readonly string[] _runtimeSettingPrefixes =
    [
        "GC",
        "Jit",
        "AltJit",
        "Tiered",
        "TC_",
        "ReadyToRun",
        "EnableHWIntrinsic",
        "EnableSSE",
        "EnableSSSE",
        "EnableAVX",
        "EnableBMI",
        "EnableFMA",
        "EnableLZCNT",
        "EnablePOPCNT",
        "EnableArm",
        "EnableAdvSimd",
        "EnableAES",
        "EnablePCLMULQDQ",
        "EnableSHA",
        "EnableCRC32",
        "EnableF16C",
        "EnableGFNI",
        "EnableMOVBE",
        "EnableVPCLMULQDQ",
        "EnableVector",
        "EnableSVE",
        "EnableDiagnostics",
        "EnableEventPipe",
    ];

    internal RunEnvironment Capture(RunStore store, CommandInvocation command, IEnumerable<ProcessConfiguration> processes)
    {
        var variables = Environment
            .GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(x => IsRelevantVariable((string)x.Key))
            .Select(x => new NamedSetting((string)x.Key, (string)x.Value!))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        var configurations = processes.Select(SnapshotProcess).ToList();
        var runtimeConfigurationPath = Path.ChangeExtension(Assembly.GetEntryAssembly()?.Location, ".runtimeconfig.json");
        configurations.Add(
            new ProcessConfiguration(
                "Collector",
                "",
                1,
                [],
                CaptureRuntimeConfiguration(store, runtimeConfigurationPath),
                [.. variables]
            )
        );
        var gc = GC.GetConfigurationVariables()
            .Select(x => new NamedSetting(
                "GC." + x.Key,
                Convert.ToString(x.Value, System.Globalization.CultureInfo.InvariantCulture)!
            ))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        gc.Add(new("GCSettings.IsServerGC", GCSettings.IsServerGC.ToString()));
        gc.Add(new("GCSettings.LatencyMode", GCSettings.LatencyMode.ToString()));
        gc.Add(new("GCSettings.LargeObjectHeapCompactionMode", GCSettings.LargeObjectHeapCompactionMode.ToString()));

        return new(
            Cpu(),
            Environment.ProcessorCount,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            ReadCommand("dotnet", command.WorkingDirectory, "--version"),
            CurrentRuntimeVersion,
            Version(typeof(DefaultConfig).Assembly),
            typeof(BenchmarkRun).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                ?? "Unavailable: assemblyにビルド構成がありません。",
            gc,
            [
                new("JitInfo", HostEnvironmentInfo.GetCurrent().JitInfo),
                new("RuntimeFeature.IsDynamicCodeSupported", RuntimeFeature.IsDynamicCodeSupported.ToString()),
                new("RuntimeFeature.IsDynamicCodeCompiled", RuntimeFeature.IsDynamicCodeCompiled.ToString()),
                new("Effective.TieredCompilation", "Unavailable: 実行processの実効値を取得する公開APIがありません。"),
                new("Effective.TieredPGO", "Unavailable: 実行processの実効値を取得する公開APIがありません。"),
                new("Effective.ReadyToRun", "Unavailable: assemblyごとの適用状態を取得する公開APIがありません。"),
            ],
            variables,
            Power(),
            command with
            {
                Arguments = [.. command.Arguments],
            },
            configurations
        );
    }

    internal ProcessConfiguration SnapshotProcess(ProcessConfiguration process)
    {
        return process with
        {
            JobSettings = [.. process.JobSettings],
            RuntimeConfiguration = [.. process.RuntimeConfiguration],
            EnvironmentVariables = [.. process.EnvironmentVariables.Where(x => IsProcessVariable(x.Name))],
        };
    }

    internal ProcessConfiguration DescribeProcess(string role, Job job)
    {
        return new(
            role,
            job.Id,
            job.Run.LaunchCount,
            [
                .. CharacteristicHelper
                    .GetAllCharacteristics(job)
                    .Where(x => !x.HasChildCharacteristics && x.Id is not "Id" and not "EnvironmentVariables")
                    .OrderBy(x => x.FullId, StringComparer.Ordinal)
                    .Select(x => new NamedSetting(
                        x.FullId,
                        job.HasValue(x)
                            ? Convert.ToString(x[job], System.Globalization.CultureInfo.InvariantCulture) ?? "null"
                            : "Default"
                    )),
            ],
            [],
            [
                .. (job.Environment.EnvironmentVariables ?? [])
                    .Where(x => IsProcessVariable(x.Key))
                    .Select(x => new NamedSetting(x.Key, x.Value)),
            ]
        );
    }

    /// <summary>
    /// 採取processのruntimeconfigを元のバイト列で保存し、比較用の設定一覧を返す
    /// </summary>
    /// <param name="store">設定ファイルの保存先</param>
    /// <param name="path">採取processのruntimeconfigのpath 特定できない場合はnull</param>
    /// <returns>保存先とSHA-256と各設定 未取得の場合は理由付きの設定1件</returns>
    internal static List<NamedSetting> CaptureRuntimeConfiguration(RunStore store, string? path)
    {
        if (!File.Exists(path))
        {
            return [new("RuntimeConfig", "Unavailable: 採取processのruntimeconfigが見つかりません。")];
        }

        var bytes = File.ReadAllBytes(path);
        var file = store.SaveFile("settings/collector.runtimeconfig.json", bytes);
        var settings = new List<NamedSetting>
        {
            new("RuntimeConfigFile.RelativePath", file.RelativePath),
            new("RuntimeConfigFile.Sha256", file.Sha256),
        };
        using var json = JsonDocument.Parse(bytes);
        Flatten(json.RootElement, "", settings);
        return settings;
    }

    private static void Flatten(JsonElement element, string prefix, List<NamedSetting> settings)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Flatten(property.Value, prefix.Length == 0 ? property.Name : prefix + "." + property.Name, settings);
            }
        }
        else
        {
            settings.Add(new(prefix, element.GetRawText()));
        }
    }

    private bool IsRelevantVariable(string name)
    {
        if (
            name.Equals("RuntimeFrameworkVersion", StringComparison.OrdinalIgnoreCase)
            || name.Equals("RollForward", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }
        foreach (var prefix in new[] { "DOTNET_", "COMPlus_" })
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var setting = name[prefix.Length..];
            return _runtimeSettingPrefixes.Any(x => setting.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                || setting
                    is "ServerGC"
                        or "ConcurrentGC"
                        or "PROCESSOR_COUNT"
                        or "ROLL_FORWARD"
                        or "ROLL_FORWARD_TO_PRERELEASE"
                        or "ROOT"
                        or "ROOT_X64"
                        or "MULTILEVEL_LOOKUP"
                        or "DiagnosticPorts"
                        or "STARTUP_HOOKS";
        }

        return false;
    }

    private bool IsProcessVariable(string name)
    {
        return IsRelevantVariable(name) || name == "WASMSHARP_BENCHMARK_RUN_CONFIG";
    }

    private static string Version(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "Unavailable: assemblyに完全版がありません。";
    }

    private static string ReadCommand(string executable, string directory, params string[] arguments)
    {
        try
        {
            return CaptureCommand.Run(executable, directory, arguments).Trim();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return "Unavailable: " + exception.Message;
        }
    }

    private static string Cpu()
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unavailable: CPU名を取得できません。";
        }

        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
        {
            return ReadLinuxCpuName(File.ReadLines("/proc/cpuinfo"));
        }

        return "Unavailable: このOSのCPU名採取には対応していません。";
    }

    internal static string ReadLinuxCpuName(IEnumerable<string> cpuInfo)
    {
        return cpuInfo.FirstOrDefault(x => x.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim()
            ?? "Unavailable: CPU名がありません。";
    }

    private static PowerConfiguration Power()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new("Unavailable", "Unavailable: このOSの電源採取には対応していません。", PowerSource.Unknown);
        }

        var source = GetSystemPowerStatus(out var status)
            ? status.AcLineStatus switch
            {
                0 => PowerSource.Battery,
                1 => PowerSource.Ac,
                _ => PowerSource.Unknown,
            }
            : PowerSource.Unknown;
        if (PowerGetActiveScheme(IntPtr.Zero, out var scheme) != 0)
        {
            return new("Unavailable", "Unavailable: 有効な電源planを取得できません。", source);
        }

        try
        {
            var id = Marshal.PtrToStructure<Guid>(scheme);
            uint size = 0;
            PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, null, ref size);
            var buffer = new byte[size];
            var result = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, buffer, ref size);
            var name =
                result == 0
                    ? System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0')
                    : "Unavailable: 電源plan名を取得できません。";
            return new(id.ToString(), name, source);
        }
        finally
        {
            LocalFree(scheme);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        internal byte AcLineStatus;
        internal byte BatteryFlag;
        internal byte BatteryLifePercent;
        internal byte SystemStatusFlag;
        internal uint BatteryLifeTime;
        internal uint BatteryFullLifeTime;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerGetActiveScheme(IntPtr rootPowerKey, out IntPtr scheme);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        ref Guid scheme,
        IntPtr subgroup,
        IntPtr setting,
        [Out] byte[]? buffer,
        ref uint size
    );

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr memory);
}
