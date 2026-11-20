namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Core 3.0のassert_returnで参照を照合する条件
/// </summary>
internal enum Core3ReferenceMatch
{
    /// <summary>
    /// 参照の種類を指定した期待値
    /// </summary>
    HeapType,

    /// <summary>
    /// null参照を指定した期待値
    /// </summary>
    Null,

    /// <summary>
    /// 番号で特定した参照との同一性を求める期待値
    /// </summary>
    Identity,
}
