namespace WasmSharp.Instructions;

/// <summary>
/// ロード命令・ストア命令の値型とアクセス幅、符号拡張の指定
/// </summary>
/// <param name="ValueType">読み書きする値のスカラー数値型</param>
/// <param name="ByteWidth">読み書きするバイト数</param>
/// <param name="SignExtend">狭い整数を読み込む場合に符号拡張するか</param>
internal readonly record struct MemoryAccessSignature(
    WasmValueKind ValueType,
    byte ByteWidth,
    bool SignExtend
);
