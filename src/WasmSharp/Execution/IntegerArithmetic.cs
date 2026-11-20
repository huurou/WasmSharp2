namespace WasmSharp.Execution;

/// <summary>
/// 関数と定数式で共有する、整数のビット幅に従った算術演算
/// </summary>
internal static class IntegerArithmetic
{
    /// <summary>
    /// 2つのi32を32ビット幅で加算する
    /// </summary>
    internal static WasmValue I32Add(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI32(unchecked(left.AsI32() + right.AsI32()));
    }

    /// <summary>
    /// 2つのi32を32ビット幅で減算する
    /// </summary>
    internal static WasmValue I32Sub(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI32(unchecked(left.AsI32() - right.AsI32()));
    }

    /// <summary>
    /// 2つのi32を32ビット幅で乗算する
    /// </summary>
    internal static WasmValue I32Mul(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI32(unchecked(left.AsI32() * right.AsI32()));
    }

    /// <summary>
    /// 2つのi64を64ビット幅で加算する
    /// </summary>
    internal static WasmValue I64Add(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI64(unchecked(left.AsI64() + right.AsI64()));
    }

    /// <summary>
    /// 2つのi64を64ビット幅で減算する
    /// </summary>
    internal static WasmValue I64Sub(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI64(unchecked(left.AsI64() - right.AsI64()));
    }

    /// <summary>
    /// 2つのi64を64ビット幅で乗算する
    /// </summary>
    internal static WasmValue I64Mul(WasmValue left, WasmValue right)
    {
        return WasmValue.FromI64(unchecked(left.AsI64() * right.AsI64()));
    }
}
