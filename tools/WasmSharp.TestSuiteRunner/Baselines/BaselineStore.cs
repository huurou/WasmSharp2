using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 完了した保存済み結果を検証し、内容を変えずにbaselineへ保存する
/// </summary>
internal static class BaselineStore
{
    /// <summary>
    /// manifestまたは実行結果を同じバイト列で保存し、指定baselineを置換する。
    /// </summary>
    /// <param name="inputPath">完了したmanifestまたは実行結果のJSONファイル</param>
    /// <param name="outputPath">置換するbaselineの保存先 入力と同じ絶対pathになる指定は受け付けない</param>
    /// <exception cref="ReportStoreException">入力と保存先の絶対pathが同じ場合、入力を読み取れない場合、記録が未完了または不整合の場合、あるいは保存に失敗した場合</exception>
    internal static void Save(string inputPath, string outputPath)
    {
        var input = Path.GetFullPath(inputPath);
        var output = Path.GetFullPath(outputPath);
        if (
            string.Equals(
                input,
                output,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal
            )
        )
        {
            throw new ReportStoreException(
                "入力自身をbaselineの保存先には指定できません。",
                output
            );
        }

        var stored = ReportStore.Read(input);
        var decision = CompletionPolicy.BaselineSave(stored.Issues, saved: true);
        if (decision.ExitCode != 0)
        {
            throw new ReportStoreException(
                $"{input}をbaselineとして保存できません: {string.Join(" ", decision.Reasons)}",
                input
            );
        }

        ReportStore.SaveBaseline(stored.Content, output);
    }
}
