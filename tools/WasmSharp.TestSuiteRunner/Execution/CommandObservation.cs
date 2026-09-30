using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// commandの処理で得た観測 判定は公開操作を再実行せず、この記録を変更しない。
/// </summary>
/// <param name="Id">処理したcommandのケース識別</param>
/// <param name="Operation">最後に行った操作 公開API以外の素材・値処理も区別する</param>
/// <param name="LastStage">最後に呼び出した公開段階 公開APIの呼び出し前はnull</param>
internal sealed record CommandObservation(CaseId Id, string Operation, CaseStage? LastStage)
{
    /// <summary>
    /// 操作が投げた例外の実体。正常完了または未実行ではnull
    /// </summary>
    internal Exception? Exception { get; init; }

    /// <summary>
    /// 現在のcommandでcallbackが投げた例外の実体。Exceptionとの同一性で発生元を判別する
    /// </summary>
    internal Exception? CallbackException { get; init; }

    /// <summary>
    /// ValueCodecで記録した実値。個数・型が期待と異なる場合も全て保持する
    /// </summary>
    internal ImmutableArray<ValueRecord> Values { get; init; } = [];

    /// <summary>
    /// ValueMatcherで比較したassert_returnの相違箇所。一致した場合は空
    /// </summary>
    internal ImmutableArray<ValueMismatch> Mismatches { get; init; } = [];

    /// <summary>
    /// 素材・JSONなどの異常。例外を伴わない場合も操作と原因を保持する
    /// </summary>
    internal ImmutableArray<CaseDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 実行に必要だった既知の利用不能状態。空の場合は既知の失敗に依存しない
    /// </summary>
    internal ImmutableArray<UnavailableCause> BlockedBy { get; init; } = [];

    /// <summary>
    /// 現在のcommandでのspectestのprint呼出順
    /// </summary>
    internal ImmutableArray<PrintRecord> Prints { get; init; } = [];
}
