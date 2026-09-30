namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// JSON内で境界を確定したcommandの範囲と、実行意味を解釈せずに取得できた項目
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Start">所有する元バイト列におけるcommandの開始位置</param>
/// <param name="Length">commandのバイト数</param>
/// <param name="Line">元入力の1始まり行番号 取得できない場合はnull</param>
/// <param name="Type">元JSONのcommand種別 取得できない場合はnull</param>
/// <param name="ModuleType">元JSONのmodule_type 未指定または取得できない場合はnull</param>
/// <param name="Filename">元JSONのfilename 未指定または取得できない場合はnull</param>
internal sealed record EnumeratedCommand(
    int Index,
    int Start,
    int Length,
    int? Line,
    string? Type,
    string? ModuleType,
    string? Filename
);
