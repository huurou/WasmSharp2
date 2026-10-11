using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 関数本体と定数式で共有する式の構文読取り
/// </summary>
internal static class ExpressionDecoder
{
    /// <summary>
    /// 式の入力命令をデコードし、最初のendを含む命令列を取得する
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
        while (reader.TryPeekByte(out var code) && code is not (0x05 or 0x0B))
        {
            var offset = reader.Position;
            reader.ReadByte();

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

            var index = immediateKind == ImmediateKind.Index ? reader.ReadU32() : 0;
            var immediate = immediateKind switch
            {
                ImmediateKind.None or ImmediateKind.Index => default,
                ImmediateKind.I32 => WasmValue.FromI32(reader.ReadS32()),
                ImmediateKind.I64 => WasmValue.FromI64(reader.ReadS64()),
                ImmediateKind.F32Bits => WasmValue.FromF32Bits(reader.ReadF32Bits()),
                ImmediateKind.F64Bits => WasmValue.FromF64Bits(reader.ReadF64Bits()),
                _ => throw new InvalidOperationException("対応済み命令の即値情報が不正です。"),
            };
            AddInstruction(
                instructions,
                new DecodedInstruction(opcode, immediate, offset, index),
                ref reader
            );
        }

        var endOffset = reader.Position;
        if (reader.ReadByte() != 0x0B)
        {
            throw reader.Error("END opcode expected: 式の終端にENDが必要です。", endOffset);
        }

        AddInstruction(
            instructions,
            new DecodedInstruction(new OpcodeKey(0, 0x0B), default, endOffset, 0),
            ref reader
        );
        return instructions;
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
