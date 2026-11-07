using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class EnvironmentCapture_SnapshotProcessTests
{
    [Test]
    public async Task 関連設定と同じ接頭辞の無関係な変数を含む_測定条件だけを値とともに保持する()
    {
        // Arrange
        string[] names =
        [
            "DOTNET_GCHeapHardLimit",
            "COMPlus_JitDisasm",
            "DOTNET_AltJit",
            "COMPlus_TieredCompilation",
            "DOTNET_TC_QuickJit",
            "COMPlus_ReadyToRun",
            "DOTNET_EnableHWIntrinsic",
            "COMPlus_EnableSSE2",
            "DOTNET_EnableSSSE3",
            "COMPlus_EnableAVX2",
            "DOTNET_EnableBMI1",
            "COMPlus_EnableFMA",
            "DOTNET_EnableLZCNT",
            "COMPlus_EnablePOPCNT",
            "DOTNET_EnableArm64Atomics",
            "COMPlus_EnableAdvSimd",
            "DOTNET_EnableAES",
            "COMPlus_EnablePCLMULQDQ",
            "DOTNET_EnableSHA",
            "COMPlus_EnableCRC32",
            "DOTNET_EnableF16C",
            "COMPlus_EnableGFNI",
            "DOTNET_EnableMOVBE",
            "COMPlus_EnableVPCLMULQDQ",
            "DOTNET_EnableVector512",
            "COMPlus_EnableSVE",
            "DOTNET_EnableDiagnostics",
            "COMPlus_EnableEventPipe",
            "DOTNET_gcServer",
            "COMPlus_gcConcurrent",
            "DOTNET_ServerGC",
            "COMPlus_ConcurrentGC",
            "DOTNET_PROCESSOR_COUNT",
            "COMPlus_ROLL_FORWARD",
            "DOTNET_ROLL_FORWARD_TO_PRERELEASE",
            "COMPlus_ROOT",
            "DOTNET_ROOT_X64",
            "COMPlus_MULTILEVEL_LOOKUP",
            "DOTNET_DiagnosticPorts",
            "COMPlus_STARTUP_HOOKS",
            "RuntimeFrameworkVersion",
            "RollForward",
            "dotnet_tieredpgo",
            "complus_enableavx",
            "WASMSHARP_BENCHMARK_RUN_CONFIG",
        ];
        var expected = names.Select((x, index) => new NamedSetting(x, "value-" + index)).ToList();
        var variables = expected
            .Concat(
                new NamedSetting[]
                {
                    new("DOTNET_PRIVATE_API_TOKEN", "dotnet secret"),
                    new("COMPlus_PRIVATE_API_TOKEN", "complus secret"),
                    new("PRIVATE_API_TOKEN", "unprefixed secret"),
                    new("DOTNET_", "empty setting"),
                    new("COMPlus_", "empty setting"),
                    new("OTHER_GCServer", "unrelated prefix"),
                }
            )
            .ToList();
        var process = new ProcessConfiguration("Diagnostic", "job", 2, [], [], variables);

        // Act
        var snapshot = EnvironmentCapture.SnapshotProcess(process);

        // Assert
        await Assert.That(snapshot.EnvironmentVariables).IsEquivalentTo(expected);
    }
}
