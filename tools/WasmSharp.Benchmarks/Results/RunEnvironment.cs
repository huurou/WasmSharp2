namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// environment.jsonに保存する実行条件 収集処理や比較時の正規化は持たない。
/// </summary>
internal sealed record RunEnvironment(
    string Cpu,
    int LogicalProcessorCount,
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    string SdkVersion,
    string RuntimeVersion,
    string BenchmarkDotNetVersion,
    string BuildConfiguration,
    List<NamedSetting> GcSettings,
    List<NamedSetting> JitSettings,
    List<NamedSetting> EnvironmentVariables,
    PowerConfiguration Power,
    CommandInvocation Command,
    List<ProcessConfiguration> Processes
);

/// <summary>
/// GC/JIT・Job・runtimeconfig・環境変数の名前と実効値
/// </summary>
internal sealed record NamedSetting(string Name, string Value);

/// <summary>
/// 測定時の電源planと給電状態
/// </summary>
internal sealed record PowerConfiguration(string PlanId, string PlanName, PowerSource PowerSource);

/// <summary>
/// 電源の接続状態 取得できない場合はUnknownとして記録する。
/// </summary>
internal enum PowerSource
{
    Ac,
    Battery,
    Unknown,
}

/// <summary>
/// 実行したコマンドと作業ディレクトリ 引数は順序を保持する。
/// </summary>
internal sealed record CommandInvocation(
    string ExecutablePath,
    List<string> Arguments,
    string WorkingDirectory
);

/// <summary>
/// 測定・診断processの構成 Job識別子と実効設定を区別して保持する。
/// </summary>
internal sealed record ProcessConfiguration(
    string Role,
    string JobId,
    int LaunchCount,
    List<NamedSetting> JobSettings,
    List<NamedSetting> RuntimeConfiguration,
    List<NamedSetting> EnvironmentVariables
);
