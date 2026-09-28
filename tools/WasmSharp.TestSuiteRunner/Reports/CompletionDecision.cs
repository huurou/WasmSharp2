using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 操作の終了状態と、合格または完了を妨げた理由
/// </summary>
/// <param name="Status">操作の終了状態</param>
/// <param name="Reasons">日本語の理由。成功時は空</param>
internal sealed record CompletionDecision(CompletionStatus Status, ImmutableArray<string> Reasons)
{
    /// <summary>
    /// プロセスの終了値
    /// </summary>
    internal int ExitCode => (int)Status;
}
