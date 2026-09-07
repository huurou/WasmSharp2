namespace WasmSharp.Modules;

/// <summary>
/// 関数exportの名前、関数indexと入力上の位置
/// </summary>
/// <remarks>関数exportの名前、参照先と入力上の位置を保持する</remarks>
/// <param name="name">export名</param>
/// <param name="functionIndex">公開対象の関数index</param>
/// <param name="byteOffset">入力バイナリ上のexport宣言のバイト位置</param>
internal readonly struct FunctionExport(string name, uint functionIndex, long byteOffset)
{
    /// <summary>
    /// 関数を公開するexport名
    /// </summary>
    internal string Name { get; } = name;

    /// <summary>
    /// moduleの関数index空間における公開対象の位置
    /// </summary>
    internal uint FunctionIndex { get; } = functionIndex;

    /// <summary>
    /// 入力バイナリ上のexport宣言のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;
}
