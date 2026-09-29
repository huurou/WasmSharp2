namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// moduleの処理段階での失敗を期待する否定assertion。直近moduleや名前の状態は更新しない
/// </summary>
/// <param name="Filename">JSONの親を基準にした素材のファイル名</param>
/// <param name="Text">加工しない期待診断</param>
/// <param name="ModuleType">素材の形式</param>
internal abstract record ModuleAssertionCommand(
    int Index,
    int? Line,
    string Filename,
    string Text,
    ScriptModuleType ModuleType
) : ScriptCommand(Index, Line);
