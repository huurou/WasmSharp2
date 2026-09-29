using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 成立しなかったcommandと、そこからたどる元の非blocked分類の失敗
/// </summary>
/// <param name="Command">名前を利用不能にした同じ入力のcommand</param>
/// <param name="Origins">Commandがblockedの場合はその元の失敗、それ以外はCommand自身</param>
internal sealed record UnavailableCause(CaseId Command, ImmutableArray<CaseId> Origins)
{
    /// <summary>
    /// 実行に必要だった利用不能状態から、blockedの直接原因と元の失敗を重複なくindex順に並べる。
    /// </summary>
    /// <param name="causes">依存した名前の利用不能の原因</param>
    internal static CaseCause CreateCaseCause(IEnumerable<UnavailableCause> causes)
    {
        var values = causes.ToArray();
        return new()
        {
            Direct = [.. values.Select(x => x.Command).Distinct().OrderBy(x => x.CommandIndex)],
            Origins =
            [
                .. values.SelectMany(x => x.Origins).Distinct().OrderBy(x => x.CommandIndex),
            ],
        };
    }
}
