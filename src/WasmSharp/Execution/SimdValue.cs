using System.Buffers.Binary;

namespace WasmSharp.Execution;

/// <summary>
/// v128のビット列とlane操作を保持する内部値
/// </summary>
internal readonly struct SimdValue
{
    /// <summary>
    /// 下位64ビット
    /// </summary>
    private readonly ulong low64_;

    /// <summary>
    /// 上位64ビット
    /// </summary>
    private readonly ulong high64_;

    /// <summary>
    /// 上下64ビットを保持する
    /// </summary>
    private SimdValue(ulong low64, ulong high64)
    {
        low64_ = low64;
        high64_ = high64;
    }

    /// <summary>
    /// v128のvalueからビット列を取得する
    /// </summary>
    internal static SimdValue FromValue(WasmValue value)
    {
        var (low64, high64) = value.AsV128();
        return new SimdValue(low64, high64);
    }

    /// <summary>
    /// 保持する128ビット列からv128のvalueを構築する
    /// </summary>
    internal WasmValue ToValue() => WasmValue.FromV128(low64_, high64_);

    /// <summary>
    /// little-endianの16バイトから128ビット列を構築する
    /// </summary>
    internal static SimdValue FromLittleEndian(ReadOnlySpan<byte> bytes)
    {
        return new SimdValue(
            BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..])
        );
    }

    /// <summary>
    /// 保持する128ビット列をlittle-endianの16バイトへ書き込む
    /// </summary>
    internal void WriteLittleEndian(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination, low64_);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], high64_);
    }

    /// <summary>
    /// 検証済みの幅とindexからlaneのビット列を取得する
    /// </summary>
    internal ulong GetLane(int bitWidth, int laneIndex)
    {
        var bitOffset = bitWidth * laneIndex;
        var half = bitOffset < 64 ? low64_ : high64_;
        // 64ビットの左シフトはシフト量が0になるため全ビット1を右シフトする
        var mask = ulong.MaxValue >> (64 - bitWidth);
        return (half >> (bitOffset % 64)) & mask;
    }

    /// <summary>
    /// 入力を検証済みのlane幅へ切り詰め対象laneだけを置換する
    /// </summary>
    internal SimdValue WithLane(int bitWidth, int laneIndex, ulong bits)
    {
        var bitOffset = bitWidth * laneIndex;
        var shift = bitOffset % 64;
        var mask = ulong.MaxValue >> (64 - bitWidth);
        var laneMask = mask << shift;
        var replacement = (bits & mask) << shift;
        return bitOffset < 64
            ? new SimdValue((low64_ & ~laneMask) | replacement, high64_)
            : new SimdValue(low64_, (high64_ & ~laneMask) | replacement);
    }
}
