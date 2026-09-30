namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Decode成功後のValidateでの検証不成立を期待するassert_invalid
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Filename">JSONの親を基準にした検証対象の素材のファイル名</param>
/// <param name="Text">Validateでの例外メッセージの先頭に期待する、加工しない診断</param>
/// <param name="ModuleType">素材の形式 textは実行対象外</param>
internal sealed record AssertInvalidCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    /// <summary>
    /// JSONの検証不成立を期待するassertionを識別するcommand種別
    /// </summary>
    internal override string Type => ASSERT_INVALID;
}
