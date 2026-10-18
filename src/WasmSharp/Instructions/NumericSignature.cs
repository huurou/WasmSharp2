using System.Collections.Immutable;

namespace WasmSharp.Instructions;

/// <summary>
/// 数値命令が取り出す入力の型列と結果の型
/// </summary>
/// <param name="InputTypes">スタック下側から上側の順序で並ぶ入力型列</param>
/// <param name="ResultType">結果の数値型</param>
internal readonly record struct NumericSignature(
    ImmutableArray<WasmValueKind> InputTypes,
    WasmValueKind ResultType
);
