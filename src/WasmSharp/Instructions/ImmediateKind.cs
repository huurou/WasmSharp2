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
    /// アラインメント指数とオフセットを表す2つのu32
    /// </summary>
    MemoryArgument,

    /// <summary>
    /// メモリインデックス0を指定する予約バイト
    /// </summary>
    MemoryZero,

    /// <summary>
    /// コピー先とコピー元のメモリインデックス0を指定する2つの予約バイト
    /// </summary>
    MemoryPairZero,

    /// <summary>
    /// データインデックスのu32とメモリインデックス0を指定する予約バイト
    /// </summary>
    DataIndexMemoryZero,

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
    /// メモリのアラインメント指数・オフセットとlaneの添字
    /// </summary>
    MemoryArgumentLaneIndex,
}
