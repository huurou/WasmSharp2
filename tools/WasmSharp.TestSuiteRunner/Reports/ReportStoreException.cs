namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 保存済み結果の読取・保存を完了できなかったことを示す例外
/// </summary>
/// <param name="message">対象pathと理由を含む説明</param>
/// <param name="path">読取・保存の対象の絶対path</param>
/// <param name="innerException">原因となった例外</param>
internal sealed class ReportStoreException(
    string message,
    string path,
    Exception? innerException = null
) : Exception(message, innerException)
{
    /// <summary>
    /// 読取・保存の対象の絶対path
    /// </summary>
    internal string Path { get; } = path;
}
