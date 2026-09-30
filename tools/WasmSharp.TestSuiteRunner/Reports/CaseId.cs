namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 入力相対pathとスクリプト内の順序による公式ケースの識別
/// </summary>
/// <param name="InputPath">test/core基準の/区切り相対path</param>
/// <param name="CommandIndex">入力内の0始まりcommand index</param>
internal sealed record CaseId(string InputPath, int CommandIndex)
{
    /// <summary>
    /// 表示用の<c>imports.wast#7</c>形式の文字列を返す。
    /// </summary>
    /// <returns>入力pathと0始まりcommand indexを#で結んだ表示用の識別</returns>
    public override string ToString()
    {
        return $"{InputPath}#{CommandIndex}";
    }
}
