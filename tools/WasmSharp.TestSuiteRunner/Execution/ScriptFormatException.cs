namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// commandが固定WABT形式の種類・構造・値として読み取れないことを示す読取中の例外
/// </summary>
/// <param name="message">不正な項目の位置と理由</param>
internal sealed class ScriptFormatException(string message) : Exception(message);
