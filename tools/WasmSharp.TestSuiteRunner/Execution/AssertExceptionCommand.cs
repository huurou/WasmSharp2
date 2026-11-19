namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// actionから未捕捉のWasm例外が送出されることを期待するCore 3.0のassert_exception
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Action">実行するinvokeまたはget</param>
internal sealed record AssertExceptionCommand(int Index, int? Line, ScriptAction Action) : ScriptCommand(Index, Line)
{
    /// <inheritdoc/>
    internal override string Type => ASSERT_EXCEPTION;
}
