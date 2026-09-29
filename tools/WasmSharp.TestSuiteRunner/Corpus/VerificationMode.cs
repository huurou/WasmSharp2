namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 素材照合の対象を分ける工程
/// </summary>
internal enum VerificationMode
{
    /// <summary>
    /// 元入力と生成物を照合する生成工程
    /// </summary>
    Generation,

    /// <summary>
    /// 元入力を前提にせず、保存した素材だけを照合する実行工程
    /// </summary>
    Execution,
}
