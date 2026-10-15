using System.Collections.Immutable;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力命令の識別子、即値とバイト位置
/// </summary>
internal readonly struct DecodedInstruction
{
    /// <summary>
    /// 通常命令またはprefix付き拡張命令の識別子
    /// </summary>
    internal OpcodeKey Opcode { get; }

    /// <summary>
    /// 命令に付随する定数の即値
    /// </summary>
    internal WasmValue Immediate { get; }

    /// <summary>
    /// 命令に埋め込まれた関数・ローカル変数・ラベルなどの番号
    /// </summary>
    internal uint Index { get; }

    /// <summary>
    /// メモリ命令のアラインメント指数とオフセット
    /// </summary>
    internal MemoryArgument MemoryArgument { get; }

    /// <summary>
    /// 構造化制御の型指定
    /// </summary>
    internal BlockType BlockType { get; }

    /// <summary>
    /// 分岐候補に続けて既定ラベルを並べた不変のラベル列
    /// </summary>
    internal ImmutableArray<uint> Labels { get; }

    /// <summary>
    /// 型注釈付きselectの不変の型列
    /// </summary>
    internal ImmutableArray<WasmValueKind> SelectTypes { get; }

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; }

    /// <summary>
    /// 即値の種類に応じた項目を保持する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <param name="immediate">定数の即値 他の命令ではdefault</param>
    /// <param name="index">添字の即値 他の命令では0</param>
    /// <param name="blockType">制御の型指定 他の命令ではdefault</param>
    /// <param name="labels">候補と既定ラベル 他の命令では非defaultの空配列</param>
    /// <param name="selectTypes">selectの型列 他の命令では非defaultの空配列</param>
    /// <param name="memoryArgument">メモリ命令の即値 他の命令ではdefault</param>
    private DecodedInstruction(
        OpcodeKey opcode,
        long byteOffset,
        WasmValue immediate = default,
        uint index = 0,
        BlockType blockType = default,
        ImmutableArray<uint> labels = default,
        ImmutableArray<WasmValueKind> selectTypes = default,
        MemoryArgument memoryArgument = default
    )
    {
        Opcode = opcode;
        ByteOffset = byteOffset;
        Immediate = immediate;
        Index = index;
        MemoryArgument = memoryArgument;
        BlockType = blockType;
        Labels = labels.IsDefault ? [] : labels;
        SelectTypes = selectTypes.IsDefault ? [] : selectTypes;
    }

    /// <summary>
    /// 即値を持たない入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>不要な即値を既定値に統一した命令</returns>
    internal static DecodedInstruction WithoutImmediate(OpcodeKey opcode, long byteOffset)
    {
        return new(opcode, byteOffset);
    }

    /// <summary>
    /// 定数の即値を持つ入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="immediate">定数の即値</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>定数以外の即値を既定値に統一した命令</returns>
    internal static DecodedInstruction WithImmediate(
        OpcodeKey opcode,
        WasmValue immediate,
        long byteOffset
    )
    {
        return new(opcode, byteOffset, immediate: immediate);
    }

    /// <summary>
    /// 添字の即値を持つ入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="index">関数・ローカル変数・ラベルなどの添字</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>添字以外の即値を既定値に統一した命令</returns>
    internal static DecodedInstruction WithIndex(OpcodeKey opcode, uint index, long byteOffset)
    {
        return new(opcode, byteOffset, index: index);
    }

    /// <summary>
    /// メモリ命令の即値を持つ入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="memoryArgument">アラインメント指数とオフセット</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>メモリ命令以外の即値を既定値に統一した命令</returns>
    internal static DecodedInstruction WithMemoryArgument(
        OpcodeKey opcode,
        MemoryArgument memoryArgument,
        long byteOffset
    )
    {
        return new(opcode, byteOffset, memoryArgument: memoryArgument);
    }

    /// <summary>
    /// 構造化制御の型指定を持つ入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="blockType">空型・単一値型・型添字のいずれか</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>型指定以外の即値を既定値に統一した命令</returns>
    internal static DecodedInstruction WithBlockType(
        OpcodeKey opcode,
        BlockType blockType,
        long byteOffset
    )
    {
        return new(opcode, byteOffset, blockType: blockType);
    }

    /// <summary>
    /// 分岐候補をコピーし、末尾に既定ラベルを加えた入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="labels">宣言順の候補ラベル 空列も許容する</param>
    /// <param name="defaultLabel">既定のラベル</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>不変のラベル列を所有する命令</returns>
    internal static DecodedInstruction WithBranchTable(
        OpcodeKey opcode,
        ReadOnlySpan<uint> labels,
        uint defaultLabel,
        long byteOffset
    )
    {
        return new(opcode, byteOffset, labels: [.. labels, defaultLabel]);
    }

    /// <summary>
    /// 型注釈付きselectの型列をコピーして入力命令を構築する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="selectTypes">符号化された型列 長さの型検証は後段で行う</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    /// <returns>不変の型列を所有する命令</returns>
    internal static DecodedInstruction WithSelectTypes(
        OpcodeKey opcode,
        ReadOnlySpan<WasmValueKind> selectTypes,
        long byteOffset
    )
    {
        return new(opcode, byteOffset, selectTypes: ImmutableArray.Create(selectTypes));
    }
}
