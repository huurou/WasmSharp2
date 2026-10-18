using WasmSharp.Instructions;

namespace WasmSharp.Execution;

/// <summary>
/// 実行可能な線形命令と即値、入力上の位置
/// </summary>
/// <remarks>検証と線形化で確定した命令を保持する</remarks>
/// <param name="opcode">命令宣言から生成された実行opcode</param>
/// <param name="immediate">命令の即値</param>
/// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
/// <param name="index">命令が参照する関数・local・分岐情報などの番号（番号を指定しない命令では参照しない）</param>
/// <param name="targetCount">分岐表の既定を除く候補数 分岐表以外では参照しない</param>
/// <param name="memoryArgument">メモリ命令のアラインメント指数とオフセット</param>
/// <param name="memoryAccessSignature">ロード命令・ストア命令のアクセス情報</param>
/// <param name="secondaryIndex">2個の添字を持つ命令の2番目の番号</param>
internal readonly struct Instruction(
    ExecutionOpcode opcode,
    WasmValue immediate,
    long byteOffset,
    uint index,
    int targetCount = 0,
    MemoryArgument memoryArgument = default,
    MemoryAccessSignature memoryAccessSignature = default,
    uint secondaryIndex = 0
)
{
    /// <summary>
    /// 命令宣言から生成された実行opcode
    /// </summary>
    internal ExecutionOpcode Opcode { get; } = opcode;

    /// <summary>
    /// 命令の即値 関数終端では参照しない
    /// </summary>
    internal WasmValue Immediate { get; } = immediate;

    /// <summary>
    /// 命令が参照する関数・local・分岐情報などの番号
    /// </summary>
    /// <remarks>単一分岐では1件、分岐表では候補列の開始番号を参照する</remarks>
    internal uint Index { get; } = index;

    /// <summary>
    /// 2個の添字を持つ命令の2番目の番号
    /// </summary>
    internal uint SecondaryIndex { get; } = secondaryIndex;

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;

    /// <summary>
    /// 分岐表の既定を除く候補数 候補がない場合は0
    /// </summary>
    internal int TargetCount { get; } = targetCount;

    /// <summary>
    /// メモリ命令のアラインメント指数とオフセット
    /// </summary>
    internal MemoryArgument MemoryArgument { get; } = memoryArgument;

    /// <summary>
    /// ロード命令・ストア命令の値型・アクセス幅・符号拡張の指定
    /// </summary>
    internal MemoryAccessSignature MemoryAccessSignature { get; } = memoryAccessSignature;
}
