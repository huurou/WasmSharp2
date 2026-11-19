namespace WasmSharp.TestSuiteRunner;

/// <summary>
/// CLIが使うOS別のパス比較と、ビルドしたwast2jsonの配置を表す。
/// </summary>
internal sealed class RunnerPaths(bool isWindows)
{
    internal static RunnerPaths Current { get; } = new RunnerPaths(OperatingSystem.IsWindows());

    internal StringComparison Comparison { get; } = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal string Wast2Json { get; } = isWindows
        ? "artifacts/wabt-core2-1.0.42-datacount/Release/wast2json.exe"
        : "artifacts/wabt-core2-1.0.42-datacount/wast2json";
}
