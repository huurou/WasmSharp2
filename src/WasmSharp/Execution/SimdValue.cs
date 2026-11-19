using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WasmSharp.Execution;

/// <summary>
/// v128のビット列とlane操作を保持する内部値
/// </summary>
internal readonly struct SimdValue
{
    private readonly Vector128<ulong> _bits;

    private SimdValue(Vector128<ulong> bits)
    {
        _bits = bits;
    }

    /// <summary>
    /// v128のvalueからビット列を取得する
    /// </summary>
    internal static SimdValue FromValue(WasmValue value)
    {
        var (low64, high64) = value.AsV128();
        return new SimdValue(Vector128.Create(low64, high64));
    }

    /// <summary>
    /// 保持する128ビット列からv128のvalueを構築する
    /// </summary>
    internal WasmValue ToValue()
    {
        return WasmValue.FromV128(_bits.GetElement(0), _bits.GetElement(1));
    }

    /// <summary>
    /// little-endianの16バイトから128ビット列を構築する
    /// </summary>
    internal static SimdValue FromLittleEndian(ReadOnlySpan<byte> bytes)
    {
        return new SimdValue(
            Vector128.Create(
                BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..])
            )
        );
    }

    /// <summary>
    /// 保持する128ビット列をlittle-endianの16バイトへ書き込む
    /// </summary>
    internal void WriteLittleEndian(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination, _bits.GetElement(0));
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], _bits.GetElement(1));
    }

    /// <summary>
    /// 検証済みの幅とindexからlaneのビット列を取得する
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ulong GetLane(int bitWidth, int laneIndex)
    {
        var index = GetNativeLaneIndex(bitWidth, laneIndex, BitConverter.IsLittleEndian);
        return bitWidth switch
        {
            8 => _bits.AsByte().GetElement(index),
            16 => _bits.AsUInt16().GetElement(index),
            32 => _bits.AsUInt32().GetElement(index),
            _ => _bits.GetElement(index),
        };
    }

    /// <summary>
    /// 入力を検証済みのlane幅へ切り詰め対象laneだけを置換する
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SimdValue WithLane(int bitWidth, int laneIndex, ulong bits)
    {
        var index = GetNativeLaneIndex(bitWidth, laneIndex, BitConverter.IsLittleEndian);
        var result = bitWidth switch
        {
            8 => _bits.AsByte().WithElement(index, (byte)bits).AsUInt64(),
            16 => _bits.AsUInt16().WithElement(index, (ushort)bits).AsUInt64(),
            32 => _bits.AsUInt32().WithElement(index, (uint)bits).AsUInt64(),
            _ => _bits.WithElement(index, bits),
        };
        return new SimdValue(result);
    }

    /// <summary>
    /// 下位64ビットと上位64ビットの順を保ち、それぞれの中のWasmのlane番号をホストの要素番号に変換する
    /// </summary>
    internal static int GetNativeLaneIndex(int bitWidth, int laneIndex, bool isLittleEndian)
    {
        return isLittleEndian ? laneIndex : laneIndex ^ (64 / bitWidth - 1);
    }

    /// <summary>
    /// 各laneに単項演算を適用し、元の幅に切り詰めた結果を返す
    /// </summary>
    internal SimdValue MapUnary(int bitWidth, Func<ulong, ulong> operation)
    {
        return bitWidth switch
        {
            8 => MapUnary<byte>(operation),
            16 => MapUnary<ushort>(operation),
            32 => MapUnary<uint>(operation),
            _ => MapUnary<ulong>(operation),
        };
    }

    /// <summary>
    /// 左右の各laneに二項演算を適用し、元の幅に切り詰めた結果を返す
    /// </summary>
    internal SimdValue MapBinary(SimdValue right, int bitWidth, Func<ulong, ulong, ulong> operation)
    {
        return bitWidth switch
        {
            8 => MapBinary<byte>(right, operation),
            16 => MapBinary<ushort>(right, operation),
            32 => MapBinary<uint>(right, operation),
            _ => MapBinary<ulong>(right, operation),
        };
    }

    private SimdValue MapUnary<T>(Func<ulong, ulong> operation)
        where T : struct, IBinaryInteger<T>
    {
        var input = _bits.As<ulong, T>();
        var result = Vector128<T>.Zero;
        // 各位置の結果を同じ位置に戻すため、ホストの要素順のまま処理できる。
        for (var index = 0; index < Vector128<T>.Count; index++)
        {
            var bits = operation(ulong.CreateTruncating(input.GetElement(index)));
            result = result.WithElement(index, T.CreateTruncating(bits));
        }
        return new SimdValue(result.AsUInt64());
    }

    private SimdValue MapBinary<T>(SimdValue right, Func<ulong, ulong, ulong> operation)
        where T : struct, IBinaryInteger<T>
    {
        var leftLanes = _bits.As<ulong, T>();
        var rightLanes = right._bits.As<ulong, T>();
        var result = Vector128<T>.Zero;
        for (var index = 0; index < Vector128<T>.Count; index++)
        {
            var leftBits = ulong.CreateTruncating(leftLanes.GetElement(index));
            var rightBits = ulong.CreateTruncating(rightLanes.GetElement(index));
            var bits = operation(leftBits, rightBits);
            result = result.WithElement(index, T.CreateTruncating(bits));
        }
        return new SimdValue(result.AsUInt64());
    }
}
