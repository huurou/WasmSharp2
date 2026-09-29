namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Decode成功後のValidateでの検証不成立を期待するassert_invalid
/// </summary>
internal sealed record AssertInvalidCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    internal override string Type => ASSERT_INVALID;
}
