namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// リンク成功後のInstantiate中の初期化・startでのtrapを期待するassert_uninstantiable
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号</param>
/// <param name="Filename">JSONの親を基準にしたインスタンス化対象の素材のファイル名</param>
/// <param name="Text">Instantiate中のtrapの例外メッセージの先頭に期待する、加工しない診断</param>
/// <param name="ModuleType">素材の形式 textは実行対象外</param>
internal sealed record AssertUninstantiableCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ModuleAssertionCommand(Index, Line, Filename, Text, ModuleType)
{
    /// <summary>
    /// JSONのインスタンス化中のtrapを期待するassertionを識別するcommand種別
    /// </summary>
    internal override string Type => ASSERT_UNINSTANTIABLE;
}
