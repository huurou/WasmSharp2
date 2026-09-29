namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// リンク成功後のInstantiate中の初期化・startでのtrapを期待するassert_uninstantiable
/// </summary>
internal sealed record AssertUninstantiableCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    internal override string Type => ASSERT_UNINSTANTIABLE;
}
