namespace WasmSharp.Modules;

/// <summary>
/// exportの名前・種類ごとの生のindexと入力位置
/// </summary>
/// <param name="name">export名</param>
/// <param name="index">指定された種類のindex空間における公開対象の位置</param>
/// <param name="byteOffset">入力バイナリ上のexport宣言のバイト位置</param>
/// <param name="kind">公開対象の外部要素の種類</param>
internal readonly struct ModuleExport(
    string name,
    uint index,
    long byteOffset,
    WasmExternalKind kind
)
{
    /// <summary>
    /// 外部要素を公開するexport名
    /// </summary>
    internal string Name { get; } = name;

    /// <summary>
    /// 指定された種類のindex空間における公開対象の位置
    /// </summary>
    internal uint Index { get; } = index;

    /// <summary>
    /// 入力バイナリ上のexport宣言のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;

    /// <summary>
    /// 公開対象の外部要素の種類
    /// </summary>
    internal WasmExternalKind Kind { get; } = kind;
}
