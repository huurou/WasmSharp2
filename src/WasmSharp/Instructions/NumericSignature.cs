namespace WasmSharp.Instructions;

/// <summary>
/// 数値命令が取り出す入力の個数と型、および結果の型
/// </summary>
/// <param name="InputCount">同じ型の入力値の個数</param>
/// <param name="InputType">入力のスカラー数値型</param>
/// <param name="ResultType">結果のスカラー数値型</param>
internal readonly record struct NumericSignature(
    byte InputCount,
    WasmValueKind InputType,
    WasmValueKind ResultType
);
