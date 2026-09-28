namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 既定値の将来変更で条件が変わらないよう、実行時に明示した実行ポリシー
/// </summary>
/// <param name="MaxCallDepth">全instanceへ指定した関数の呼び出し深さの上限</param>
internal sealed record RunExecutionPolicy(int MaxCallDepth);
