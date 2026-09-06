namespace WasmSharp;

/// <summary>
/// 実行時のオプション設定
/// </summary>
/// <param name="MaxCallDepth">関数の呼び出し深さの上限</param>
public sealed record WasmExecutionOptions(int MaxCallDepth);
