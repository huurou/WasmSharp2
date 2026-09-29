using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 境界を確定できたが、固定形式の種類・構造・値として読み取れないcommand
/// </summary>
/// <remarks>
/// 1件のrunner_errorとして記録し、後続のcommandは続行する。読み取れた種類と更新対象だけを保持し、取得できない名前は推定しない。
/// </remarks>
/// <param name="Type">取得できたcommand種別。固定形式外の種別も含み、取得できない場合はnull</param>
/// <param name="Diagnostic">異常理由と元JSONを含む診断</param>
internal sealed record InvalidCommand(int Index, int? Line, string? Type, CaseDiagnostic Diagnostic)
    : ScriptCommand(Index, Line)
{
    internal override string? Type { get; } = Type;

    /// <summary>
    /// 通常moduleで読み取れたmodule識別子。通常module以外と、取得できない場合はnull
    /// </summary>
    internal string? Name { get; init; }

    /// <summary>
    /// registerで読み取れた登録名。register以外と、取得できない場合はnull
    /// </summary>
    internal string? As { get; init; }
}
