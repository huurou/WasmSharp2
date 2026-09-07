using System.Buffers.Binary;
using System.Text;
using WasmSharp.Exceptions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力上の位置を保持して限定されたバイト範囲を読む
/// </summary>
internal ref struct WasmBinaryReader
{
    private static readonly UTF8Encoding utf8_ = new(false, true);
    private readonly ReadOnlySpan<byte> bytes_;
    private readonly long startOffset_;
    private int position_;

    internal readonly long Position => startOffset_ + position_;
    internal readonly int Remaining => bytes_.Length - position_;
    internal byte? SectionId { get; set; }
    internal uint? FunctionIndex { get; }

    internal WasmBinaryReader(
        ReadOnlySpan<byte> bytes,
        long startOffset = 0,
        byte? sectionId = null,
        uint? functionIndex = null
    )
    {
        bytes_ = bytes;
        startOffset_ = startOffset;
        position_ = 0;
        SectionId = sectionId;
        FunctionIndex = functionIndex;
    }

    internal byte ReadByte()
    {
        return ReadBytes(1)[0];
    }

    internal ReadOnlySpan<byte> ReadBytes(uint length)
    {
        if (length > (uint)Remaining)
        {
            throw Error("宣言された長さが入力の残量を超えています。");
        }

        var result = bytes_.Slice(position_, (int)length);
        position_ += (int)length;
        return result;
    }

    internal WasmBinaryReader ReadRange(uint length, uint? functionIndex = null)
    {
        var start = Position;
        return new WasmBinaryReader(
            ReadBytes(length),
            start,
            SectionId,
            functionIndex ?? FunctionIndex
        );
    }

    internal uint ReadU32()
    {
        return (uint)ReadInteger(32, false);
    }

    internal int ReadS32()
    {
        return unchecked((int)ReadInteger(32, true));
    }

    internal long ReadS64()
    {
        return unchecked((long)ReadInteger(64, true));
    }

    internal uint ReadF32Bits()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
    }

    internal ulong ReadF64Bits()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
    }

    internal string ReadName()
    {
        var length = ReadU32();
        var offset = Position;
        var bytes = ReadBytes(length);
        try
        {
            return utf8_.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new WasmDecodeException("名前のUTF-8が不正です。", Location(offset), exception);
        }
    }

    private ulong ReadInteger(int width, bool signed)
    {
        ulong result = 0;
        for (var shift = 0; shift < width; shift += 7)
        {
            var offset = Position;
            var next = ReadByte();
            var payload = next & 0x7F;
            var remainingBits = width - shift;
            if (remainingBits < 7)
            {
                // 最終バイトの余ったビットは、符号なしなら0、符号付きなら符号拡張に限る。
                var positiveLimit = 1 << (signed ? remainingBits - 1 : remainingBits);
                var validPayload =
                    payload < positiveLimit || (signed && payload >= 128 - positiveLimit);
                if ((next & 0x80) != 0 || !validPayload)
                {
                    throw Error("整数の最大幅または未使用ビットが不正です。", offset);
                }
            }

            result |= (ulong)payload << shift;
            if ((next & 0x80) == 0)
            {
                if (signed && (next & 0x40) != 0 && shift + 7 < 64)
                {
                    result |= ulong.MaxValue << (shift + 7);
                }

                return result;
            }
        }

        throw Error("整数が終端していません。");
    }

    internal readonly WasmFailureLocation Location(long? offset = null)
    {
        return new WasmFailureLocation(
            WasmProcessingStage.Decode,
            offset ?? Position,
            FunctionIndex,
            SectionId
        );
    }

    internal readonly WasmDecodeException Error(string message, long? offset = null)
    {
        return new WasmDecodeException(message, Location(offset), null);
    }
}
