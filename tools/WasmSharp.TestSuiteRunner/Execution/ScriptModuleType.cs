namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// commandが参照するmodule素材の形式
/// </summary>
internal enum ScriptModuleType
{
    /// <summary>
    /// 実行対象のbinary
    /// </summary>
    Binary,

    /// <summary>
    /// 実行処理では開かないtext
    /// </summary>
    Text,
}
