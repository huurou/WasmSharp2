using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal partial class EnvironmentCapture_CaptureTests
{
    private readonly CaseJson _json = new CaseJson();
    private readonly EnvironmentCapture _environmentCapture = new EnvironmentCapture();

    [Test]
    [NotInParallel]
    public async Task 環境変数を逆順に追加する_保存する設定を名前順に並べる()
    {
        // Arrange
        const string FIRST_VARIABLE = "DOTNET_GC_CUSTOM_A";
        const string LAST_VARIABLE = "DOTNET_GC_CUSTOM_Z";
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, _json);
        var command = new CommandInvocation(Environment.ProcessPath!, [], Environment.CurrentDirectory);
        var previousFirst = Environment.GetEnvironmentVariable(FIRST_VARIABLE);
        var previousLast = Environment.GetEnvironmentVariable(LAST_VARIABLE);
        try
        {
            Environment.SetEnvironmentVariable(LAST_VARIABLE, "z");
            Environment.SetEnvironmentVariable(FIRST_VARIABLE, "a");

            // Act
            var captured = _environmentCapture.Capture(store, command, []);

            // Assert
            var names = captured.EnvironmentVariables.Select(x => x.Name);
            using (Assert.Multiple())
            {
                await Assert.That(names.SequenceEqual(names.Order(StringComparer.Ordinal))).IsTrue();
                await Assert.That(captured.EnvironmentVariables.Single(x => x.Name == FIRST_VARIABLE).Value).IsEqualTo("a");
                await Assert.That(captured.EnvironmentVariables.Single(x => x.Name == LAST_VARIABLE).Value).IsEqualTo("z");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(FIRST_VARIABLE, previousFirst);
            Environment.SetEnvironmentVariable(LAST_VARIABLE, previousLast);
        }
    }

    [Test]
    public async Task 現在の採取processを記録する_GCとJITと保存したruntimeconfigの識別情報を保持する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, _json);
        var command = new CommandInvocation(Environment.ProcessPath!, [], Environment.CurrentDirectory);
        var windowsBefore = OperatingSystem.IsWindows() ? await ReadWindowsEnvironment() : null;

        // Act
        var captured = _environmentCapture.Capture(store, command, []);
        var windowsAfter = OperatingSystem.IsWindows() ? await ReadWindowsEnvironment() : null;

        // Assert
        var collector = captured.Processes.Single();
        var gcNames = captured.GcSettings.Where(x => x.Name.StartsWith("GC.", StringComparison.Ordinal)).Select(x => x.Name);
        var runtimeConfig = File.ReadAllBytes(
            Path.ChangeExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location, ".runtimeconfig.json")
        );
        using (Assert.Multiple())
        {
            await Assert.That(gcNames.SequenceEqual(gcNames.Order(StringComparer.Ordinal))).IsTrue();
            if (windowsBefore is not null && windowsAfter is not null)
            {
                await Assert.That(windowsBefore.CpuNames).Contains(captured.Cpu);
                if (windowsBefore.PowerPlan == windowsAfter.PowerPlan)
                {
                    var plan = PowerPlanRegex().Match(windowsBefore.PowerPlan);
                    await Assert.That(plan.Success).IsTrue();
                    await Assert.That(captured.Power.PlanId).IsEqualTo(Guid.Parse(plan.Groups["id"].Value).ToString());
                    await Assert.That(captured.Power.PlanName).IsEqualTo(plan.Groups["name"].Value);
                }
            }
            await Assert.That(collector.Role).IsEqualTo("Collector");
            await Assert.That(collector.JobId).IsEqualTo("");
            await Assert.That(collector.LaunchCount).IsEqualTo(1);
            await Assert.That(collector.JobSettings).IsEmpty();
            await Assert.That(collector.EnvironmentVariables).IsEquivalentTo(captured.EnvironmentVariables);
            await Assert
                .That(collector.RuntimeConfiguration.Single(x => x.Name == "RuntimeConfigFile.RelativePath").Value)
                .IsEqualTo("settings/collector.runtimeconfig.json");
            await Assert
                .That(collector.RuntimeConfiguration.Single(x => x.Name == "RuntimeConfigFile.Sha256").Value)
                .IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(runtimeConfig)));
            await Assert
                .That(
                    File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, "settings/collector.runtimeconfig.json"))
                        .SequenceEqual(runtimeConfig)
                )
                .IsTrue();
            await Assert.That(captured.RuntimeVersion).StartsWith(RuntimeInformation.FrameworkDescription + "; ");
            await Assert.That(Version.TryParse(captured.SdkVersion.Split('-')[0], out _)).IsTrue();
            await Assert
                .That(captured.GcSettings.Single(x => x.Name == "GCSettings.LatencyMode").Value)
                .IsEqualTo(GCSettings.LatencyMode.ToString());
            await Assert
                .That(captured.GcSettings.Single(x => x.Name == "GCSettings.LargeObjectHeapCompactionMode").Value)
                .IsEqualTo(GCSettings.LargeObjectHeapCompactionMode.ToString());
            foreach (var setting in GC.GetConfigurationVariables())
            {
                await Assert
                    .That(captured.GcSettings.Single(x => x.Name == "GC." + setting.Key).Value)
                    .IsEqualTo(Convert.ToString(setting.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            await Assert.That(captured.JitSettings.Single(x => x.Name == "JitInfo").Value.Length > 0).IsTrue();
            await Assert
                .That(captured.JitSettings.Single(x => x.Name == "RuntimeFeature.IsDynamicCodeSupported").Value)
                .IsEqualTo(RuntimeFeature.IsDynamicCodeSupported.ToString());
            await Assert
                .That(captured.JitSettings.Single(x => x.Name == "RuntimeFeature.IsDynamicCodeCompiled").Value)
                .IsEqualTo(RuntimeFeature.IsDynamicCodeCompiled.ToString());
            await Assert
                .That(captured.JitSettings.Single(x => x.Name == "Effective.TieredPGO").Value)
                .StartsWith("Unavailable:");
            await Assert
                .That(captured.JitSettings.Single(x => x.Name == "Effective.ReadyToRun").Value)
                .StartsWith("Unavailable:");
        }
    }

    [Test]
    public async Task SDK採取の作業ディレクトリが存在しない_取得不能の診断を記録する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, _json);
        var command = new CommandInvocation(Environment.ProcessPath!, [], Path.Combine(fixture.DirectoryPath, "missing"));

        // Act
        var captured = _environmentCapture.Capture(store, command, []);

        // Assert
        await Assert.That(captured.SdkVersion).StartsWith("Unavailable: ");
    }

    private static async Task<WindowsEnvironment> ReadWindowsEnvironment()
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell/v1.0/powershell.exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(
            """
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $cpuNames = @(Get-CimInstance -ClassName Win32_Processor | ForEach-Object { $_.Name.Trim() })
            $powerPlan = & (Join-Path $env:SystemRoot 'System32/powercfg.exe') /getactivescheme
            if ($LASTEXITCODE -ne 0) { throw 'powercfgの読み取りに失敗しました。' }
            [pscustomobject]@{ CpuNames = $cpuNames; PowerPlan = ($powerPlan -join "`n").Trim() } | ConvertTo-Json -Compress
            """
        );
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // OS情報の採取が応答しない場合も、検証用プロセスを残さない。
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"Windows環境の採取が終了しませんでした。\n{await error}");
        }
        await Assert.That(process.ExitCode).IsEqualTo(0);
        await Assert.That(await error).IsEmpty();
        return JsonSerializer.Deserialize<WindowsEnvironment>(await output)!;
    }

    private sealed record WindowsEnvironment(List<string> CpuNames, string PowerPlan);

    [GeneratedRegex(@"(?<id>[0-9a-fA-F-]{36})\s+\((?<name>.*)\)$")]
    private static partial Regex PowerPlanRegex();
}
