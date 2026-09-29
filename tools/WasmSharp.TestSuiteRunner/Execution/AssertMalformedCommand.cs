namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Decodeでの構文不成立を期待するassert_malformed
/// </summary>
internal sealed record AssertMalformedCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    internal override string Type => ASSERT_MALFORMED;
}
