namespace WasmSharp;

/// <summary>
/// valueを表現するクラス
/// </summary>
public readonly struct WasmValue
{
    /// <summary>
    /// v128の下位64ビットまたはスカラー値
    /// </summary>
    private readonly ulong low64_;

    /// <summary>
    /// v128の上位64ビット
    /// </summary>
    private readonly ulong high64_;

    /// <summary>
    /// 参照値の参照先
    /// </summary>
    private readonly object? reference_;

    /// <summary>
    /// 格納されたvalueの種類
    /// </summary>
    private readonly WasmValueKind kind_;

    /// <summary>
    /// 格納されたvalueの種類
    /// </summary>
    public WasmValueKind Kind => kind_;

    private WasmValue(
        WasmValueKind kind,
        ulong low64 = 0,
        ulong high64 = 0,
        object? reference = null
    )
    {
        kind_ = kind;
        low64_ = low64;
        high64_ = high64;
        reference_ = reference;
    }

    /// <summary>
    /// 32ビット整数からvalueを構築する
    /// </summary>
    public static WasmValue FromI32(int value)
    {
        return new WasmValue(WasmValueKind.I32, unchecked((uint)value));
    }

    /// <summary>
    /// 32ビット整数を取得する
    /// </summary>
    public int AsI32()
    {
        EnsureKind(WasmValueKind.I32);
        return unchecked((int)low64_);
    }

    /// <summary>
    /// 64ビット整数からvalueを構築する
    /// </summary>
    public static WasmValue FromI64(long value)
    {
        return new WasmValue(WasmValueKind.I64, unchecked((ulong)value));
    }

    /// <summary>
    /// 64ビット整数を取得する
    /// </summary>
    public long AsI64()
    {
        EnsureKind(WasmValueKind.I64);
        return unchecked((long)low64_);
    }

    /// <summary>
    /// 32ビット浮動小数点数からビット列を保持してvalueを構築する
    /// </summary>
    public static WasmValue FromF32(float value)
    {
        return FromF32Bits(BitConverter.SingleToUInt32Bits(value));
    }

    /// <summary>
    /// ビット列を指定して32ビット浮動小数点数のvalueを構築する
    /// </summary>
    public static WasmValue FromF32Bits(uint bits)
    {
        return new WasmValue(WasmValueKind.F32, bits);
    }

    /// <summary>
    /// 32ビット浮動小数点数を取得する
    /// </summary>
    public float AsF32()
    {
        return BitConverter.UInt32BitsToSingle(AsF32Bits());
    }

    /// <summary>
    /// 32ビット浮動小数点数のビット列を取得する
    /// </summary>
    public uint AsF32Bits()
    {
        EnsureKind(WasmValueKind.F32);
        return (uint)low64_;
    }

    /// <summary>
    /// 64ビット浮動小数点数からビット列を保持してvalueを構築する
    /// </summary>
    public static WasmValue FromF64(double value)
    {
        return FromF64Bits(BitConverter.DoubleToUInt64Bits(value));
    }

    /// <summary>
    /// ビット列を指定して64ビット浮動小数点数のvalueを構築する
    /// </summary>
    public static WasmValue FromF64Bits(ulong bits)
    {
        return new WasmValue(WasmValueKind.F64, bits);
    }

    /// <summary>
    /// 64ビット浮動小数点数を取得する
    /// </summary>
    public double AsF64()
    {
        return BitConverter.UInt64BitsToDouble(AsF64Bits());
    }

    /// <summary>
    /// 64ビット浮動小数点数のビット列を取得する
    /// </summary>
    public ulong AsF64Bits()
    {
        EnsureKind(WasmValueKind.F64);
        return low64_;
    }

    /// <summary>
    /// 上下64ビットを指定して128ビットベクトルのvalueを構築する
    /// </summary>
    public static WasmValue FromV128(ulong low64, ulong high64)
    {
        return new WasmValue(WasmValueKind.V128, low64, high64);
    }

    /// <summary>
    /// 128ビットベクトルの上下64ビットを取得する
    /// </summary>
    public (ulong Low64, ulong High64) AsV128()
    {
        EnsureKind(WasmValueKind.V128);
        return (low64_, high64_);
    }

    /// <summary>
    /// 関数への参照またはnullからfuncrefのvalueを構築する
    /// </summary>
    public static WasmValue FromFuncRef(WasmFunction? value)
    {
        return new WasmValue(WasmValueKind.FuncRef, reference: value);
    }

    /// <summary>
    /// funcrefの参照先またはnullを取得する
    /// </summary>
    public WasmFunction? AsFuncRef()
    {
        EnsureKind(WasmValueKind.FuncRef);
        return (WasmFunction?)reference_;
    }

    /// <summary>
    /// CLRオブジェクトへの参照またはnullからexternrefのvalueを構築する
    /// </summary>
    public static WasmValue FromExternRef(object? value)
    {
        return new WasmValue(WasmValueKind.ExternRef, reference: value);
    }

    /// <summary>
    /// externrefの元のCLRオブジェクトへの参照またはnullを取得する
    /// </summary>
    public object? AsExternRef()
    {
        EnsureKind(WasmValueKind.ExternRef);
        return reference_;
    }

    private void EnsureKind(WasmValueKind expected)
    {
        if (kind_ != expected)
        {
            throw new InvalidOperationException(
                $"{kind_}のvalueを{expected}として取得できません。"
            );
        }
    }
}
