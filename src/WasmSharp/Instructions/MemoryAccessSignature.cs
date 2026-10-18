namespace WasmSharp.Instructions;

/// <summary>
/// ロード命令・ストア命令の値型とアクセス幅、符号拡張の指定
/// </summary>
/// <param name="ValueType">読み書きする値の数値型 v128を含む</param>
/// <param name="ByteWidth">メモリで読み書きするバイト数 結果の値幅とは独立する</param>
/// <param name="SignExtend">狭いスカラー整数を読み込む場合に符号拡張するか vectorではfalse</param>
internal readonly record struct MemoryAccessSignature(
    WasmValueKind ValueType,
    byte ByteWidth,
    bool SignExtend
);
