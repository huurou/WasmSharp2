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
internal static class EnvironmentCapture
{
    internal static RunEnvironment Capture(
        RunStore store,
        CommandInvocation command,
        IEnumerable<ProcessConfiguration> processes
    )
    {
        var variables = Environment
            .GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(x => IsRelevantVariable((string)x.Key))
            .Select(x => new NamedSetting((string)x.Key, (string)x.Value!))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        var configurations = processes.Select(SnapshotProcess).ToList();
        configurations.Add(
            new("Collector", "", 1, [], RuntimeConfiguration(store), variables.ToList())
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
        gc.Add(
            new(
                "GCSettings.LargeObjectHeapCompactionMode",
                GCSettings.LargeObjectHeapCompactionMode.ToString()
            )
        );

        return new(
            Cpu(),
            Environment.ProcessorCount,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            ReadCommand("dotnet", command.WorkingDirectory, "--version"),
            RuntimeInformation.FrameworkDescription + "; " + Version(typeof(object).Assembly),
            Version(typeof(DefaultConfig).Assembly),
            typeof(BenchmarkRun)
                .Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()
                ?.Configuration
                ?? "Unavailable: assemblyにビルド構成がありません。",
            gc,
            [
                new("JitInfo", HostEnvironmentInfo.GetCurrent().JitInfo),
                new(
                    "RuntimeFeature.IsDynamicCodeSupported",
                    RuntimeFeature.IsDynamicCodeSupported.ToString()
                ),
                new(
                    "RuntimeFeature.IsDynamicCodeCompiled",
                    RuntimeFeature.IsDynamicCodeCompiled.ToString()
                ),
                new(
                    "Effective.TieredCompilation",
                    "Unavailable: 実行processの実効値を取得する公開APIがありません。"
                ),
                new(
                    "Effective.TieredPGO",
                    "Unavailable: 実行processの実効値を取得する公開APIがありません。"
                ),
                new(
                    "Effective.ReadyToRun",
                    "Unavailable: assemblyごとの適用状態を取得する公開APIがありません。"
                ),
            ],
            variables,
            Power(),
            command with
            {
                Arguments = command.Arguments.ToList(),
            },
            configurations
        );
    }

    internal static ProcessConfiguration SnapshotProcess(ProcessConfiguration process) =>
        process with
        {
            JobSettings = process.JobSettings.ToList(),
            RuntimeConfiguration = process.RuntimeConfiguration.ToList(),
            EnvironmentVariables = process
                .EnvironmentVariables.Where(x => IsProcessVariable(x.Name))
                .ToList(),
        };

    internal static ProcessConfiguration DescribeProcess(string role, Job job) =>
        new(
            role,
            job.Id,
            job.Run.LaunchCount,
            CharacteristicHelper
                .GetAllCharacteristics(job)
                .Where(x =>
                    !x.HasChildCharacteristics && x.Id is not "Id" and not "EnvironmentVariables"
                )
                .OrderBy(x => x.FullId, StringComparer.Ordinal)
                .Select(x => new NamedSetting(
                    x.FullId,
                    job.HasValue(x)
                        ? Convert.ToString(
                            x[job],
                            System.Globalization.CultureInfo.InvariantCulture
                        ) ?? "null"
                        : "Default"
                ))
                .ToList(),
            [],
            (job.Environment.EnvironmentVariables ?? [])
                .Where(x => IsProcessVariable(x.Key))
                .Select(x => new NamedSetting(x.Key, x.Value))
                .ToList()
        );

    private static List<NamedSetting> RuntimeConfiguration(RunStore store)
    {
        var entry = Assembly.GetEntryAssembly()?.Location;
        var path = entry is null ? null : Path.ChangeExtension(entry, ".runtimeconfig.json");
        if (path is null || !File.Exists(path))
        {
            return
            [
                new("RuntimeConfig", "Unavailable: 採取processのruntimeconfigが見つかりません。"),
            ];
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
                Flatten(
                    property.Value,
                    prefix.Length == 0 ? property.Name : prefix + "." + property.Name,
                    settings
                );
            }
        }
        else
        {
            settings.Add(new(prefix, element.GetRawText()));
        }
    }

    private static bool IsRelevantVariable(string name)
    {
        foreach (var prefix in new[] { "DOTNET_", "COMPlus_" })
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var setting = name[prefix.Length..];
            return new[]
                {
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
                }.Any(x => setting.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                || setting
                    is "gcServer"
                        or "gcConcurrent"
                        or "ServerGC"
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

    private static bool IsProcessVariable(string name) =>
        IsRelevantVariable(name) || name == "WASMSHARP_BENCHMARK_RUN_CONFIG";

    private static string Version(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "Unavailable: assemblyに完全版がありません。";

    private static string ReadCommand(
        string executable,
        string directory,
        params string[] arguments
    )
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
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"
            );
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim()
                ?? "Unavailable: CPU名を取得できません。";
        }

        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
        {
            return File.ReadLines("/proc/cpuinfo")
                    .FirstOrDefault(x => x.StartsWith("model name", StringComparison.Ordinal))
                    ?.Split(':', 2)[1]
                    .Trim()
                ?? "Unavailable: CPU名がありません。";
        }

        return "Unavailable: このOSのCPU名採取には対応していません。";
    }

    private static PowerConfiguration Power()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(
                "Unavailable",
                "Unavailable: このOSの電源採取には対応していません。",
                PowerSource.Unknown
            );
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
            var result = PowerReadFriendlyName(
                IntPtr.Zero,
                ref id,
                IntPtr.Zero,
                IntPtr.Zero,
                buffer,
                ref size
            );
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

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr rootPowerKey, out IntPtr scheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        ref Guid scheme,
        IntPtr subgroup,
        IntPtr setting,
        [Out] byte[]? buffer,
        ref uint size
    );

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
