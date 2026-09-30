using System.Buffers.Binary;
using System.Text;
using WasmSharp.Exceptions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力上の位置を保持して限定されたバイト範囲を読む
/// </summary>
internal ref struct ModuleBinaryReader
{
    /// <summary>
    /// 不正なバイト列を置換せず、診断として扱う名前読み取り用のUTF-8符号化
    /// </summary>
    private static readonly UTF8Encoding utf8_ = new(false, true);

    /// <summary>
    /// この読み取り範囲の入力バイト列
    /// </summary>
    private readonly ReadOnlySpan<byte> bytes_;

    /// <summary>
    /// 入力バイナリ上の読み取り範囲の先頭位置
    /// </summary>
    private readonly long startOffset_;

    /// <summary>
    /// 読み取り範囲の先頭から数えた次の読み取り位置
    /// </summary>
    private int position_;

    /// <summary>
    /// Decode開始位置を0とした、次の読み取り位置
    /// </summary>
    internal readonly long Position => startOffset_ + position_;

    /// <summary>
    /// この範囲に残る未読み取りのバイト数
    /// </summary>
    internal readonly int Remaining => bytes_.Length - position_;

    /// <summary>
    /// 診断に使用するsection ID section外ではnull
    /// </summary>
    internal byte? SectionId { get; set; }

    /// <summary>
    /// 診断に使用するmodule全体の関数index 関数外ではnull
    /// </summary>
    internal uint? FunctionIndex { get; }

    /// <summary>
    /// 入力の参照と診断位置を保持し、指定範囲の先頭から読める状態を構築する
    /// </summary>
    /// <remarks>入力バイト列はコピーしないため、読み取り中は入力内容を変更しない</remarks>
    /// <param name="bytes">この読み取り範囲の入力バイト列</param>
    /// <param name="startOffset">Decode開始位置を0とした、この範囲の先頭位置</param>
    /// <param name="sectionId">診断に使用するsection ID section外ではnull</param>
    /// <param name="functionIndex">診断に使用するmodule全体の関数index 関数外ではnull</param>
    internal ModuleBinaryReader(
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

    /// <summary>
    /// 現在位置の1バイトを読み取り、位置を進める
    /// </summary>
    /// <returns>現在位置にあったバイト</returns>
    /// <exception cref="WasmDecodeException">読み取り範囲の終端に達している場合</exception>
    internal byte ReadByte()
    {
        return ReadBytes(1)[0];
    }

    /// <summary>
    /// 指定長の入力を参照し、その範囲の終端まで位置を進める
    /// </summary>
    /// <param name="length">読み取るバイト数</param>
    /// <returns>元の入力バイト列を参照する範囲 コピーは作成しない</returns>
    /// <exception cref="WasmDecodeException">指定長がこの範囲の残量を超える場合</exception>
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

    /// <summary>
    /// 指定長の部分範囲を独立して読む状態を作り、元の読み取り位置をその終端へ進める
    /// </summary>
    /// <param name="length">部分範囲のバイト数</param>
    /// <param name="functionIndex">部分範囲の関数index nullの場合は現在の関数indexを引き継ぐ</param>
    /// <returns>入力の参照、入力上の先頭位置とsection IDを引き継ぐ部分範囲の読み取り状態</returns>
    /// <exception cref="WasmDecodeException">指定長がこの範囲の残量を超える場合</exception>
    internal ModuleBinaryReader ReadRange(uint length, uint? functionIndex = null)
    {
        var start = Position;
        return new ModuleBinaryReader(
            ReadBytes(length),
            start,
            SectionId,
            functionIndex ?? FunctionIndex
        );
    }

    /// <summary>
    /// u32のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号なし32ビット整数</returns>
    /// <exception cref="WasmDecodeException">終端、最大幅または未使用ビットの符号化が不正な場合</exception>
    internal uint ReadU32()
    {
        return (uint)ReadInteger(32, false);
    }

    /// <summary>
    /// s32のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号付き32ビット整数</returns>
    /// <exception cref="WasmDecodeException">終端、最大幅または符号拡張の符号化が不正な場合</exception>
    internal int ReadS32()
    {
        return unchecked((int)ReadInteger(32, true));
    }

    /// <summary>
    /// s64のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号付き64ビット整数</returns>
    /// <exception cref="WasmDecodeException">終端、最大幅または符号拡張の符号化が不正な場合</exception>
    internal long ReadS64()
    {
        return unchecked((long)ReadInteger(64, true));
    }

    /// <summary>
    /// f32のリトルエンディアン符号化を、浮動小数点演算を行わずに読み取る
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する32ビット列</returns>
    /// <exception cref="WasmDecodeException">読み取り範囲に4バイト残っていない場合</exception>
    internal uint ReadF32Bits()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
    }

    /// <summary>
    /// f64のリトルエンディアン符号化を、浮動小数点演算を行わずに読み取る
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する64ビット列</returns>
    /// <exception cref="WasmDecodeException">読み取り範囲に8バイト残っていない場合</exception>
    internal ulong ReadF64Bits()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
    }

    /// <summary>
    /// バイト数付きのnameを読み取り、UTF-8として正しい文字列であることを確認する
    /// </summary>
    /// <returns>デコードした名前 空文字列も許容する</returns>
    /// <exception cref="WasmDecodeException">長さの符号化、入力範囲またはUTF-8が不正な場合</exception>
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

    /// <summary>
    /// 指定した整数幅のLEB128を読み取り、終端と未使用ビットを検査する
    /// </summary>
    /// <param name="width">整数のビット幅 32または64</param>
    /// <param name="signed">符号付きとして符号拡張を行う場合はtrue</param>
    /// <returns>整数のビット列 符号付きの場合は64ビットまで符号拡張する</returns>
    /// <exception cref="WasmDecodeException">入力が不足するか、終端、最大幅または未使用ビットが不正な場合</exception>
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

    /// <summary>
    /// デコードの診断に使う入力位置と、現在の関数・sectionの情報をまとめる
    /// </summary>
    /// <param name="offset">Decode開始位置を0とした診断位置 nullの場合は現在の読み取り位置</param>
    /// <returns>Decode段階の診断位置</returns>
    internal readonly WasmFailureLocation Location(long? offset = null)
    {
        return new WasmFailureLocation(
            WasmProcessingStage.Decode,
            offset ?? Position,
            FunctionIndex,
            SectionId
        );
    }

    /// <summary>
    /// 入力位置、関数とsectionの情報を持つ構文エラーを作成する
    /// </summary>
    /// <param name="message">構文エラーの内容</param>
    /// <param name="offset">Decode開始位置を0とした診断位置 nullの場合は現在の読み取り位置</param>
    /// <returns>原因例外を持たないデコード例外 このメソッドでは送出しない</returns>
    internal readonly WasmDecodeException Error(string message, long? offset = null)
    {
        return new WasmDecodeException(message, Location(offset), null);
    }
}
