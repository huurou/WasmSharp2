namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Decode・Validate成功後のInstantiateでのリンク不成立を期待するassert_unlinkable
/// </summary>
internal sealed record AssertUnlinkableCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    internal override string Type => ASSERT_UNLINKABLE;
}
