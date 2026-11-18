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
    private readonly ulong _low64;

    /// <summary>
    /// 上位64ビット
    /// </summary>
    private readonly ulong _high64;

    /// <summary>
    /// 上下64ビットを保持する
    /// </summary>
    private SimdValue(ulong low64, ulong high64)
    {
        _low64 = low64;
        _high64 = high64;
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
    internal WasmValue ToValue()
    {
        return WasmValue.FromV128(_low64, _high64);
    }

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
        BinaryPrimitives.WriteUInt64LittleEndian(destination, _low64);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], _high64);
    }

    /// <summary>
    /// 検証済みの幅とindexからlaneのビット列を取得する
    /// </summary>
    internal ulong GetLane(int bitWidth, int laneIndex)
    {
        var bitOffset = bitWidth * laneIndex;
        var half = bitOffset < 64 ? _low64 : _high64;
        // 64ビットの左シフトはシフト量が0になるため全ビット1を右シフトする
        var mask = ulong.MaxValue >> (64 - bitWidth);
        return (half >> bitOffset) & mask;
    }

    /// <summary>
    /// 入力を検証済みのlane幅へ切り詰め対象laneだけを置換する
    /// </summary>
    internal SimdValue WithLane(int bitWidth, int laneIndex, ulong bits)
    {
        var bitOffset = bitWidth * laneIndex;
        var mask = ulong.MaxValue >> (64 - bitWidth);
        var laneMask = mask << bitOffset;
        var replacement = (bits & mask) << bitOffset;
        return bitOffset < 64
            ? new SimdValue((_low64 & ~laneMask) | replacement, _high64)
            : new SimdValue(_low64, (_high64 & ~laneMask) | replacement);
    }
}
