using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 一つの公式入力の処理状態と、処理したcommandの結果
/// </summary>
/// <param name="InputPath">test/core基準の/区切り相対path</param>
internal sealed record InputRunResult(string InputPath)
{
    /// <summary>
    /// 入力の処理状態。開始前は未処理
    /// </summary>
    public InputRunStatus Status { get; init; } = InputRunStatus.Unprocessed;

    /// <summary>
    /// 確定したcommand総数。列挙できない、または実行前の照合で信用できない場合はnull
    /// </summary>
    public int? CommandCount { get; init; }

    /// <summary>
    /// 境界を確定できたcommand数
    /// </summary>
    public int EnumeratedCount { get; init; }

    /// <summary>
    /// 列挙済みのうち、中断などにより処理していない末尾のcommand数
    /// </summary>
    public int UnprocessedCount { get; init; }

    /// <summary>
    /// command単位の件数とは別に数える、素材照合・JSON読取などの入力異常
    /// </summary>
    public List<CorpusDiagnostic> Issues { get; init; } = [];

    /// <summary>
    /// 処理したcommandの結果。未処理のcommandは含めない
    /// </summary>
    public List<CaseResult> Cases { get; init; } = [];
}
