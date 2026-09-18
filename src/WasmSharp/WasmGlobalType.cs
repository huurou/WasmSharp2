namespace WasmSharp;

/// <summary>
/// globalの値型と可変性
/// </summary>
/// <param name="ValueKind">globalに保持する値の型</param>
/// <param name="IsMutable">値を更新できるかどうか</param>
public sealed record WasmGlobalType(WasmValueKind ValueKind, bool IsMutable);
