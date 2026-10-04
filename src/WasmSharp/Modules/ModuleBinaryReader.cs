using System.Buffers.Binary;
using System.Text;
using WasmSharp.Exceptions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力上の位置と宣言終端を保持し、通常解析または失敗診断用の範囲を読む
/// </summary>
/// <remarks>通常解析の境界不正はModuleReadBoundaryException、診断用読取りの物理EOFはWasmDecodeExceptionとして通知する</remarks>
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
    /// 宣言範囲を越えるbyteを診断に使用できるかを示す読取りモード
    /// </summary>
    private readonly ModuleReadMode mode_;

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
    /// 原入力の物理終端
    /// </summary>
    internal long InputEnd { get; }

    /// <summary>
    /// この読取り範囲の宣言終端
    /// </summary>
    internal long DeclaredEnd { get; }

    /// <summary>
    /// 現在位置から宣言終端までの残量
    /// </summary>
    internal readonly long DeclaredRemaining => DeclaredEnd - Position;

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
    /// <param name="mode">通常解析または境界失敗後の診断用読取り</param>
    internal ModuleBinaryReader(
        ReadOnlySpan<byte> bytes,
        long startOffset = 0,
        byte? sectionId = null,
        uint? functionIndex = null,
        ModuleReadMode mode = ModuleReadMode.Bounded
    )
        : this(
            bytes,
            startOffset,
            startOffset + bytes.Length,
            startOffset + bytes.Length,
            sectionId,
            functionIndex,
            mode
        ) { }

    /// <summary>
    /// モードに応じた入力参照と、物理終端・宣言終端を保持する
    /// </summary>
    /// <param name="bytes">この読取り範囲の入力バイト列</param>
    /// <param name="startOffset">Decode開始位置を0とした、この範囲の先頭位置</param>
    /// <param name="inputEnd">原入力の物理終端</param>
    /// <param name="declaredEnd">この範囲の宣言終端</param>
    /// <param name="sectionId">診断に使用するsection ID</param>
    /// <param name="functionIndex">診断に使用するmodule全体の関数index</param>
    /// <param name="mode">通常解析または境界失敗後の診断用読取り</param>
    private ModuleBinaryReader(
        ReadOnlySpan<byte> bytes,
        long startOffset,
        long inputEnd,
        long declaredEnd,
        byte? sectionId,
        uint? functionIndex,
        ModuleReadMode mode
    )
    {
        bytes_ = bytes;
        startOffset_ = startOffset;
        InputEnd = inputEnd;
        DeclaredEnd = declaredEnd;
        mode_ = mode;
        position_ = 0;
        SectionId = sectionId;
        FunctionIndex = functionIndex;
    }

    /// <summary>
    /// 現在位置の1バイトを読み取り、位置を進める
    /// </summary>
    /// <returns>現在位置にあったバイト</returns>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲の終端に達している場合</exception>
    /// <exception cref="WasmDecodeException">診断用読取りで物理EOFに達している場合</exception>
    internal byte ReadByte()
    {
        return ReadBytes(1)[0];
    }

    /// <summary>
    /// 読取り位置を進めず、現在範囲の次byteを確認する
    /// </summary>
    /// <param name="value">次byte 範囲の終端では0</param>
    /// <returns>次byteがある場合はtrue</returns>
    internal readonly bool TryPeekByte(out byte value)
    {
        value = Remaining == 0 ? (byte)0 : bytes_[position_];
        return Remaining != 0;
    }

    /// <summary>
    /// 指定長の入力を参照し、その範囲の終端まで位置を進める
    /// </summary>
    /// <param name="length">読み取るバイト数</param>
    /// <returns>元の入力バイト列を参照する範囲 コピーは作成しない</returns>
    /// <exception cref="ModuleReadBoundaryException">通常解析で指定長がこの範囲の残量を超える場合</exception>
    /// <exception cref="WasmDecodeException">診断用読取りで指定長が物理残量を超える場合</exception>
    internal ReadOnlySpan<byte> ReadBytes(uint length)
    {
        if (
            (long)length > Remaining
            || (mode_ == ModuleReadMode.Bounded && (long)length > DeclaredRemaining)
        )
        {
            throw BoundaryError(
                "unexpected end of section or function: 宣言された長さを取得できません。"
            );
        }

        var result = bytes_.Slice(position_, (int)length);
        position_ += (int)length;
        return result;
    }

    /// <summary>
    /// 指定長の宣言範囲を独立して読む状態を作り、物理終端を超えずに元の読み取り位置を進める
    /// </summary>
    /// <remarks>診断用読取りでは子が物理終端までを参照し、親は宣言長と物理残量の小さい方だけ進む</remarks>
    /// <param name="length">部分範囲のバイト数</param>
    /// <param name="functionIndex">部分範囲の関数index nullの場合は現在の関数indexを引き継ぐ</param>
    /// <returns>入力の参照、入力上の先頭位置とsection IDを引き継ぐ部分範囲の読み取り状態</returns>
    /// <exception cref="ModuleReadBoundaryException">通常解析で指定長がこの範囲の残量を超える場合</exception>
    internal ModuleBinaryReader ReadRange(uint length, uint? functionIndex = null)
    {
        var start = Position;
        if (mode_ == ModuleReadMode.Diagnostic)
        {
            // 子は物理終端まで参照し、親は宣言長と物理残量の小さい方だけ進める。
            var bytes = bytes_.Slice(position_);
            position_ += (int)Math.Min((long)length, Remaining);
            return new ModuleBinaryReader(
                bytes,
                start,
                InputEnd,
                start + length,
                SectionId,
                functionIndex ?? FunctionIndex,
                mode_
            );
        }

        return new ModuleBinaryReader(
            ReadBytes(length),
            start,
            InputEnd,
            start + length,
            SectionId,
            functionIndex ?? FunctionIndex,
            mode_
        );
    }

    /// <summary>
    /// u32のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号なし32ビット整数</returns>
    /// <exception cref="WasmDecodeException">最大幅・未使用ビットの符号化が不正か、診断用読取りで整数の途中に物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲の終端に整数が未完了の場合</exception>
    internal uint ReadU32()
    {
        return (uint)ReadInteger(32, false);
    }

    /// <summary>
    /// 長さを読み、prefix前の位置から物理終端までの上限を確認する
    /// </summary>
    /// <returns>物理入力による上限以内の宣言長 取得時の宣言範囲検査は別に行う</returns>
    /// <exception cref="WasmDecodeException">長さの符号化または物理上限が不正か、診断用読取りで長さの途中に物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で長さの符号化を限定範囲内で取得できない場合</exception>
    internal uint ReadLength()
    {
        var offset = Position;
        var length = ReadU32();
        if ((long)length > InputEnd - offset)
        {
            throw Error("length out of bounds: 宣言長が物理入力の上限を超えています。", offset);
        }

        return length;
    }

    /// <summary>
    /// u1のLEB128符号化を読み取る
    /// </summary>
    /// <returns>0または1</returns>
    /// <exception cref="WasmDecodeException">整数の符号化が不正か、診断用読取りで物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で整数を限定範囲内で取得できない場合</exception>
    internal uint ReadU1()
    {
        return (uint)ReadInteger(1, false);
    }

    /// <summary>
    /// s7のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号付き7ビット整数</returns>
    /// <exception cref="WasmDecodeException">整数の符号化が不正か、診断用読取りで物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で整数を限定範囲内で取得できない場合</exception>
    internal int ReadS7()
    {
        return unchecked((int)ReadInteger(7, true));
    }

    /// <summary>
    /// s32のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号付き32ビット整数</returns>
    /// <exception cref="WasmDecodeException">最大幅・符号拡張の符号化が不正か、診断用読取りで整数の途中に物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲の終端に整数が未完了の場合</exception>
    internal int ReadS32()
    {
        return unchecked((int)ReadInteger(32, true));
    }

    /// <summary>
    /// s64のLEB128符号化を読み取る
    /// </summary>
    /// <returns>符号付き64ビット整数</returns>
    /// <exception cref="WasmDecodeException">最大幅・符号拡張の符号化が不正か、診断用読取りで整数の途中に物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲の終端に整数が未完了の場合</exception>
    internal long ReadS64()
    {
        return unchecked((long)ReadInteger(64, true));
    }

    /// <summary>
    /// f32のリトルエンディアン符号化を、浮動小数点演算を行わずに読み取る
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する32ビット列</returns>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲に4バイト残っていない場合</exception>
    /// <exception cref="WasmDecodeException">診断用読取りで物理入力に4バイト残っていない場合</exception>
    internal uint ReadF32Bits()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
    }

    /// <summary>
    /// f64のリトルエンディアン符号化を、浮動小数点演算を行わずに読み取る
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する64ビット列</returns>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲に8バイト残っていない場合</exception>
    /// <exception cref="WasmDecodeException">診断用読取りで物理入力に8バイト残っていない場合</exception>
    internal ulong ReadF64Bits()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
    }

    /// <summary>
    /// バイト数付きのnameを読み取り、UTF-8として正しい文字列であることを確認する
    /// </summary>
    /// <returns>デコードした名前 空文字列も許容する</returns>
    /// <exception cref="WasmDecodeException">長さ・UTF-8が不正か、診断用読取りで名前を物理入力内で取得できない場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で名前の長さまたはbyte列を限定範囲内で取得できない場合</exception>
    internal string ReadName()
    {
        var length = ReadLength();
        var offset = Position;
        var bytes = ReadBytes(length);
        try
        {
            return utf8_.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new WasmDecodeException(
                "malformed UTF-8 encoding: 名前のUTF-8が不正です。",
                Location(offset),
                exception
            );
        }
    }

    /// <summary>
    /// 指定した整数幅のLEB128を読み取り、終端と未使用ビットを検査する
    /// </summary>
    /// <param name="width">整数のビット幅</param>
    /// <param name="signed">符号付きとして符号拡張を行う場合はtrue</param>
    /// <returns>整数のビット列 符号付きの場合は64ビットまで符号拡張する</returns>
    /// <exception cref="WasmDecodeException">最大幅・未使用ビットが不正か、診断用読取りで整数の途中に物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で読み取り範囲の終端に整数が未完了の場合</exception>
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
                if (!validPayload)
                {
                    throw Error("integer too large: 整数の未使用ビットが不正です。", offset);
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

        throw Error("integer representation too long: 整数の表現長が幅を超えています。");
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

    /// <summary>
    /// 確定した範囲不正を、通常解析の内部通知または診断用の構文エラーにする
    /// </summary>
    /// <param name="message">範囲不正の内容</param>
    /// <param name="offset">Decode開始位置を0とした診断位置 nullの場合は現在の読み取り位置</param>
    /// <returns>モードに応じた例外 このメソッドでは送出しない</returns>
    internal readonly Exception BoundaryError(string message, long? offset = null)
    {
        var error = Error(message, offset);
        return mode_ == ModuleReadMode.Diagnostic ? error : new ModuleReadBoundaryException(error);
    }
}
