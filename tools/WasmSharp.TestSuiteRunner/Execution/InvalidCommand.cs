using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 境界を確定できたが、固定形式の種類・構造・値として読み取れないcommand
/// </summary>
/// <remarks>
/// 1件のrunner_errorとして記録し、後続のcommandは続行する。読み取れた種類と更新対象だけを保持し、取得できない名前は推定しない。
/// </remarks>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号 取得できない場合はnull</param>
/// <param name="Type">取得できたcommand種別 固定形式外の種別も含み、取得できない場合はnull</param>
/// <param name="Diagnostic">異常理由と元JSONを含む診断</param>
internal sealed record InvalidCommand(int Index, int? Line, string? Type, CaseDiagnostic Diagnostic)
    : ScriptCommand(Index, Line)
{
    /// <summary>
    /// 不正なJSONから取得できたcommand種別 取得できない場合はnull
    /// </summary>
    internal override string? Type { get; } = Type;

    /// <summary>
    /// 未知のcommandを集計区分から除くための仕様版
    /// </summary>
    internal WasmSpecVersion SpecVersion { get; init; } = WasmSpecVersion.Core2;

    internal override CaseCategory? Category => GetCategory(Type, SpecVersion);

    /// <summary>
    /// 通常module・module定義で読み取れた名前、またはmodule_instanceで読み取れたinstanceのmodule識別子 それ以外の種別と、取得できない場合はnull
    /// </summary>
    internal string? Name { get; init; }

    /// <summary>
    /// registerで読み取れた登録名 register以外と、取得できない場合はnull
    /// </summary>
    internal string? As { get; init; }
}
