namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// commandの種類・構造・値が変換プロファイルのJSON形式に合わないことを示す読取中の例外
/// </summary>
/// <param name="message">不正な項目の位置と理由</param>
internal sealed class ScriptFormatException(string message) : Exception(message);
