namespace WasmSharp.Instructions;

/// <summary>
/// 命令の即値の符号化
/// </summary>
internal enum ImmediateKind
{
    /// <summary>
    /// 即値の読み取りが未対応
    /// </summary>
    Unsupported,

    /// <summary>
    /// 即値なし
    /// </summary>
    None,

    /// <summary>
    /// 符号付きLEB128で符号化された32ビット整数
    /// </summary>
    I32,

    /// <summary>
    /// 符号付きLEB128で符号化された64ビット整数
    /// </summary>
    I64,

    /// <summary>
    /// リトルエンディアンで符号化された32ビット浮動小数点数のビット列
    /// </summary>
    F32Bits,

    /// <summary>
    /// リトルエンディアンで符号化された64ビット浮動小数点数のビット列
    /// </summary>
    F64Bits,

    /// <summary>
    /// 符号なしLEB128で符号化された32ビット添字
    /// </summary>
    Index,

    /// <summary>
    /// 空型、単一の値型またはs33の型添字で符号化された制御型
    /// </summary>
    BlockType,

    /// <summary>
    /// 候補ラベルのベクトルと既定ラベル
    /// </summary>
    BranchTable,

    /// <summary>
    /// selectの型注釈を表す値型のベクトル
    /// </summary>
    TypeVector,

    /// <summary>
    /// Core 2.0ではアラインメント指数とオフセットをu32で順に表す。
    /// Core 3.0では先頭のu32フラグの下位6ビットがアラインメント指数を表す。
    /// ビット6が立っている場合はu32のメモリインデックスが続き、最後にu64のオフセットを置く。
    /// </summary>
    MemoryArgument,

    /// <summary>
    /// メモリインデックス Core 2.0では0を表す予約バイト
    /// </summary>
    MemoryIndex,

    /// <summary>
    /// コピー先とコピー元のメモリインデックス
    /// </summary>
    MemoryIndexPair,

    /// <summary>
    /// データインデックスと、その後に続くメモリインデックス
    /// </summary>
    DataAndMemoryIndex,

    /// <summary>
    /// データインデックスを表すu32
    /// </summary>
    DataIndex,

    /// <summary>
    /// funcrefまたはexternrefを指定する参照型
    /// </summary>
    ReferenceType,

    /// <summary>
    /// 符号なしLEB128で符号化された2個の32ビット添字
    /// </summary>
    IndexPair,

    /// <summary>
    /// リトルエンディアンで符号化された128ビットの定数
    /// </summary>
    V128,

    /// <summary>
    /// 32未満の選択値を持つ16個のbyte 検証前は範囲を制限しない
    /// </summary>
    Shuffle,

    /// <summary>
    /// laneの添字を表す1byte
    /// </summary>
    LaneIndex,

    /// <summary>
    /// <see cref="MemoryArgument"/>に続けて1バイトのlane番号を指定する。
    /// </summary>
    MemoryArgumentLaneIndex,
}
