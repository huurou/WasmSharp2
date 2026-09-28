namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 比較できなかった対象と理由
/// </summary>
/// <param name="Target">対象の入力・生成物・ケース・条件の表示名</param>
/// <param name="Reason">比較できなかった理由</param>
internal sealed record UncomparedItem(string Target, string Reason);
