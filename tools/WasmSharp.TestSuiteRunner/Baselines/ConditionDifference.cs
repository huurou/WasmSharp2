namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 名前で識別する一つの条件の前後の値
/// </summary>
/// <param name="Name">条件の名前</param>
/// <param name="Baseline">比較元の値。存在しない場合はnull</param>
/// <param name="Current">現結果の値。存在しない場合はnull</param>
internal sealed record ConditionDifference(string Name, string? Baseline, string? Current);
