namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 期待値と実際の結果の相違の種類
/// </summary>
internal enum ValueMismatchKind
{
    /// <summary>
    /// 結果の個数
    /// </summary>
    Count,

    /// <summary>
    /// 同じ位置の結果の型
    /// </summary>
    Type,

    /// <summary>
    /// 同じ位置・同じ型の結果の値
    /// </summary>
    Value,
}
