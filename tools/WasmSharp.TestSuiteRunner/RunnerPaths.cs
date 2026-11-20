namespace WasmSharp.TestSuiteRunner;

/// <summary>
/// CLIが使うOS別のパス比較と、配布版wasm-toolsの配置例を表す。
/// </summary>
internal sealed class RunnerPaths(bool isWindows)
{
    internal static RunnerPaths Current { get; } = new RunnerPaths(OperatingSystem.IsWindows());

    internal StringComparison Comparison { get; } =
        isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal string WasmTools { get; } =
        isWindows
            ? "artifacts/wasm-tools-1.263.0-x86_64-windows/wasm-tools.exe"
            : "artifacts/wasm-tools-1.263.0-x86_64-linux/wasm-tools";
}
