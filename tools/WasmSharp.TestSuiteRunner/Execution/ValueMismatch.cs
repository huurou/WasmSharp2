namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 期待値と実際の結果の1箇所の相違
/// </summary>
/// <param name="Kind">相違の種類</param>
/// <param name="Index">相違した結果の0始まり位置 個数の相違ではnull</param>
/// <param name="Lane">相違したv128の0始まりlane v128のlane以外の相違ではnull</param>
/// <param name="Message">期待と実際を示す相違の内容</param>
internal sealed record ValueMismatch(ValueMismatchKind Kind, int? Index, int? Lane, string Message);
