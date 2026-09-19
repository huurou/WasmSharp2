namespace WasmSharp.Modules.Definitions;

/// <summary>
/// tableの参照型、未検証のlimitsと入力位置
/// </summary>
/// <param name="ElementKind">table要素の参照型</param>
/// <param name="Limits">未検証の要素数の最小値と任意の最大値</param>
/// <param name="ByteOffset">入力バイナリ上のtable型記述のバイト位置</param>
internal sealed record TableDefinition(
    WasmValueKind ElementKind,
    WasmLimits Limits,
    long ByteOffset
);
