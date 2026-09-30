namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Decode・Validate成功後のInstantiateでのリンク不成立を期待するassert_unlinkable
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Filename">JSONの親を基準にしたリンク対象の素材のファイル名</param>
/// <param name="Text">リンク不成立の例外メッセージの先頭に期待する、加工しない診断</param>
/// <param name="ModuleType">素材の形式 textは実行対象外</param>
internal sealed record AssertUnlinkableCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    /// <summary>
    /// JSONのリンク不成立を期待するassertionを識別するcommand種別
    /// </summary>
    internal override string Type => ASSERT_UNLINKABLE;
}
