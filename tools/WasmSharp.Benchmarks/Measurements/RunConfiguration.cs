using System.Collections.Immutable;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 子processへ渡すrunとJobの識別情報 設定のpathは環境変数で渡す
/// </summary>
internal sealed record RunConfiguration(
    string RunId,
    string OutputDirectory,
    string AssetManifestPath,
    string JobId,
    List<string> PlannedCaseIds
)
{
    internal const string ENVIRONMENT_VARIABLE = "WASMSHARP_BENCHMARK_RUN_CONFIG";
}

/// <summary>
/// 新規runの素材・予定ケース・実行条件
/// </summary>
internal sealed record RunRequest(
    string OutputDirectory,
    string AssetManifestPath,
    List<string> CaseIds,
    RunKind Kind,
    IConfig Config,
    string RepositoryDirectory,
    CommandInvocation Command
);

/// <summary>
/// 予定と子process設定の保存を終えた実行境界
/// </summary>
internal sealed record PreparedRun(RunStore Store, string RunId, ImmutableArray<Job> Jobs);
