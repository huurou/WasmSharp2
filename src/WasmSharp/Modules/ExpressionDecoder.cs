using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 関数本体と定数式で共有する式の構文読取り
/// </summary>
internal static class ExpressionDecoder
{
    /// <summary>
    /// 式の入力命令をデコードし、最外のendを含む命令列を取得する
    /// </summary>
    /// <remarks>命令の型検証と線形実行コードの生成は行わない</remarks>
    /// <param name="reader">式の先頭にある読み取り状態 endの直後まで進める</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <returns>即値、indexと入力上の位置を持つ、endまでの対応済み命令列</returns>
    /// <exception cref="WasmDecodeException">命令や即値の符号化が不正か、対応するifのないelseがある場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられないか、endが欠落している場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">割り当て済みの未実装命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">命令数がコレクションの保持上限を超える場合</exception>
    internal static List<DecodedInstruction> Read(ref ModuleBinaryReader reader, int inputLength)
    {
        List<DecodedInstruction> instructions = [];
        // ifのthenを読み終えたframeはelseのopcodeで保持し、重複elseを区別する。
        List<byte> controls = [];
        while (true)
        {
            var offset = reader.Position;
            var code = reader.ReadByte();

            var opcode = code is 0xFC or 0xFD
                ? new OpcodeKey(code, reader.ReadU32())
                : new OpcodeKey(0, code);
            if (!InstructionSet.TryGet(opcode, out var descriptor))
            {
                // 固定参照は0xFCだけprefixを表示し、0xFDは命令番号だけを表示する。
                var opcodeText =
                    opcode.Prefix == 0xFC
                        ? $"{opcode.Prefix:x2} {opcode.Code:x2}"
                        : $"{opcode.Code:x2}";
                throw reader.Error(
                    $"illegal opcode {opcodeText}: Core 2.0に割り当てられていないopcodeです。",
                    offset
                );
            }

            var immediateKind = descriptor.Immediate;
            if (immediateKind == ImmediateKind.Unsupported)
            {
                throw reader.Unsupported(descriptor.Name, inputLength, offset);
            }

            var instruction = immediateKind switch
            {
                ImmediateKind.None => DecodedInstruction.WithoutImmediate(opcode, offset),
                ImmediateKind.Index => DecodedInstruction.WithIndex(
                    opcode,
                    reader.ReadU32(),
                    offset
                ),
                ImmediateKind.I32 => DecodedInstruction.WithImmediate(
                    opcode,
                    WasmValue.FromI32(reader.ReadS32()),
                    offset
                ),
                ImmediateKind.I64 => DecodedInstruction.WithImmediate(
                    opcode,
                    WasmValue.FromI64(reader.ReadS64()),
                    offset
                ),
                ImmediateKind.F32Bits => DecodedInstruction.WithImmediate(
                    opcode,
                    WasmValue.FromF32Bits(reader.ReadF32Bits()),
                    offset
                ),
                ImmediateKind.F64Bits => DecodedInstruction.WithImmediate(
                    opcode,
                    WasmValue.FromF64Bits(reader.ReadF64Bits()),
                    offset
                ),
                ImmediateKind.BlockType => DecodedInstruction.WithBlockType(
                    opcode,
                    ReadBlockType(ref reader),
                    offset
                ),
                ImmediateKind.BranchTable => ReadBranchTable(opcode, offset, ref reader),
                ImmediateKind.TypeVector => ReadSelectTypes(opcode, offset, ref reader),
                ImmediateKind.MemoryArgument => DecodedInstruction.WithMemoryArgument(
                    opcode,
                    new(reader.ReadU32(), reader.ReadU32()),
                    offset
                ),
                ImmediateKind.MemoryZero => ReadMemoryZero(opcode, offset, ref reader),
                _ => throw new InvalidOperationException("対応済み命令の即値情報が不正です。"),
            };

            if (code == 0x05)
            {
                if (controls.Count == 0 || controls[^1] != 0x04)
                {
                    throw reader.Error("END opcode expected: 式の終端にENDが必要です。", offset);
                }

                controls[^1] = 0x05;
            }

            AddInstruction(instructions, instruction, ref reader);
            if (code is 0x02 or 0x03 or 0x04)
            {
                controls.Add(code);
            }
            else if (code == 0x0B)
            {
                if (controls.Count == 0)
                {
                    return instructions;
                }

                controls.RemoveAt(controls.Count - 1);
            }
        }
    }

    /// <summary>
    /// メモリインデックス0を指定する単一の予約バイトを読む
    /// </summary>
    /// <param name="opcode">メモリ命令の識別子</param>
    /// <param name="offset">命令の入力位置</param>
    /// <param name="reader">予約バイトの先頭にある読み取り状態</param>
    /// <returns>予約バイトの構文を検査した命令</returns>
    private static DecodedInstruction ReadMemoryZero(
        OpcodeKey opcode,
        long offset,
        ref ModuleBinaryReader reader
    )
    {
        if (reader.ReadByte() != 0)
        {
            throw reader.Error(
                "zero byte expected: 予約バイトには0x00が必要です。",
                reader.Position - 1
            );
        }

        return DecodedInstruction.WithoutImmediate(opcode, offset);
    }

    /// <summary>
    /// 単一byteの空型・値型と、非負s33の型添字を区別して取得する
    /// </summary>
    /// <param name="reader">制御型の先頭にある読取り状態 即値の終端まで進める</param>
    /// <returns>存在や引数・結果の適合を未検証の制御型</returns>
    private static BlockType ReadBlockType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        if (reader.TryPeekByte(out var code))
        {
            if (code == 0x40)
            {
                reader.ReadByte();
                return BlockType.Empty;
            }

            if (code is >= 0x40 and < 0x80)
            {
                return BlockType.FromValue(ModuleBinaryFormat.ReadValueType(ref reader));
            }
        }

        var typeIndex = reader.ReadS33();
        if (typeIndex < 0)
        {
            // 固定参照が負のs33を拒否した後、s7として先頭byteを読む場合と同じ診断位置
            throw reader.Error(
                "integer representation too long: 制御型の値型は単一byteで符号化します。",
                offset + 1
            );
        }

        return BlockType.FromTypeIndex((uint)typeIndex);
    }

    /// <summary>
    /// 分岐表の候補を読み、末尾の既定ラベルとともに不変の即値へ保存する
    /// </summary>
    /// <param name="opcode">分岐表命令の識別子</param>
    /// <param name="offset">命令の入力位置</param>
    /// <param name="reader">候補vectorの先頭にある読取り状態 既定ラベルの終端まで進める</param>
    /// <returns>候補と既定ラベルを保持する入力命令</returns>
    private static DecodedInstruction ReadBranchTable(
        OpcodeKey opcode,
        long offset,
        ref ModuleBinaryReader reader
    )
    {
        var countOffset = reader.Position;
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        if (count == Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "既定を含むラベル数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                reader.Location(countOffset)
            );
        }

        List<uint> labels = [];
        for (uint index = 0; index < count; index++)
        {
            labels.Add(reader.ReadU32());
        }

        var defaultLabel = reader.ReadU32();
        return DecodedInstruction.WithBranchTable(
            opcode,
            CollectionsMarshal.AsSpan(labels),
            defaultLabel,
            offset
        );
    }

    /// <summary>
    /// typed selectの型列を読み、長さの適格性を判定せず不変の即値へ保存する
    /// </summary>
    /// <param name="opcode">typed select命令の識別子</param>
    /// <param name="offset">命令の入力位置</param>
    /// <param name="reader">型vectorの先頭にある読取り状態 即値の終端まで進める</param>
    /// <returns>宣言順の値型を保持する入力命令</returns>
    private static DecodedInstruction ReadSelectTypes(
        OpcodeKey opcode,
        long offset,
        ref ModuleBinaryReader reader
    )
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<WasmValueKind> types = [];
        for (uint index = 0; index < count; index++)
        {
            types.Add(ModuleBinaryFormat.ReadValueType(ref reader));
        }

        return DecodedInstruction.WithSelectTypes(opcode, CollectionsMarshal.AsSpan(types), offset);
    }

    /// <summary>
    /// 保持上限を確認してデコード済み命令を追加する
    /// </summary>
    /// <param name="instructions">式の命令を保持する一覧</param>
    /// <param name="instruction">追加する命令</param>
    /// <param name="reader">診断位置に使用する読取り状態 位置は変更しない</param>
    private static void AddInstruction(
        List<DecodedInstruction> instructions,
        DecodedInstruction instruction,
        ref ModuleBinaryReader reader
    )
    {
        if (instructions.Count == Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "命令数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                reader.Location(instruction.ByteOffset)
            );
        }

        instructions.Add(instruction);
    }
}
