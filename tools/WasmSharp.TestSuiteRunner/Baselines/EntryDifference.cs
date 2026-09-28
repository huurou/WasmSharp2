namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 一つの入力または生成物の属性の前後の値
/// </summary>
/// <param name="Path">入力のtest/core基準、または生成物のmanifest基準の相対path</param>
/// <param name="Property">差がある属性</param>
/// <param name="Baseline">比較元の値。存在しない場合はnull</param>
/// <param name="Current">現結果の値。存在しない場合はnull</param>
internal sealed record EntryDifference(
    string Path,
    string Property,
    string? Baseline,
    string? Current
);
