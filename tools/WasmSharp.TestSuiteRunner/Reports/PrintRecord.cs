namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// spectestのprint系関数の1回の呼出し
/// </summary>
/// <param name="Function">呼ばれた関数名</param>
internal sealed record PrintRecord(string Function)
{
    /// <summary>
    /// 呼出し順の引数の型とビット列
    /// </summary>
    public List<ValueRecord> Arguments { get; init; } = [];
}
