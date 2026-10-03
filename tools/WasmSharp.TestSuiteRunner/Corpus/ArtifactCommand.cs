namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成JSON内で境界を確定できた一つのcommand
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号 取得できない場合はnull</param>
/// <param name="Type">元JSONのcommand種別 取得できない場合はnull</param>
/// <param name="ModuleType">元JSONのmodule_type 未指定または取得できない場合はnull</param>
/// <param name="Filename">元JSONのfilename 未指定または取得できない場合はnull</param>
internal sealed record ArtifactCommand(
    int Index,
    int? Line,
    string? Type,
    string? ModuleType,
    string? Filename
);
