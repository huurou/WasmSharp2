namespace WasmSharp;

/// <summary>
/// Wasmの型付きvalueを保持する値型
/// </summary>
public readonly struct WasmValue
{
    /// <summary>
    /// 数値スカラーのビット列またはv128の下位64ビット
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

    /// <summary>
    /// valueの型と対応するビット列または参照先を保持する
    /// </summary>
    /// <param name="kind">保持するvalueの型</param>
    /// <param name="low64">数値スカラーのビット列またはv128の下位64ビット 参照型では0</param>
    /// <param name="high64">v128の上位64ビット 他の型では0</param>
    /// <param name="reference">参照型の参照先またはnull 数値型ではnull</param>
    private WasmValue(WasmValueKind kind, ulong low64, ulong high64, object? reference)
    {
        kind_ = kind;
        low64_ = low64;
        high64_ = high64;
        reference_ = reference;
    }

    /// <summary>
    /// 32ビット整数からvalueを構築する
    /// </summary>
    /// <param name="value">保持する32ビット整数</param>
    /// <returns>同じ32ビット列を保持するi32のvalue</returns>
    public static WasmValue FromI32(int value)
    {
        return new WasmValue(WasmValueKind.I32, unchecked((uint)value), 0, null);
    }

    /// <summary>
    /// 32ビット整数を取得する
    /// </summary>
    /// <returns>保持するビット列を符号付き32ビット整数として解釈した値</returns>
    /// <exception cref="InvalidOperationException">valueの型がi32ではない場合</exception>
    public int AsI32()
    {
        EnsureKind(WasmValueKind.I32);
        return unchecked((int)low64_);
    }

    /// <summary>
    /// 64ビット整数からvalueを構築する
    /// </summary>
    /// <param name="value">保持する64ビット整数</param>
    /// <returns>同じ64ビット列を保持するi64のvalue</returns>
    public static WasmValue FromI64(long value)
    {
        return new WasmValue(WasmValueKind.I64, unchecked((ulong)value), 0, null);
    }

    /// <summary>
    /// 64ビット整数を取得する
    /// </summary>
    /// <returns>保持するビット列を符号付き64ビット整数として解釈した値</returns>
    /// <exception cref="InvalidOperationException">valueの型がi64ではない場合</exception>
    public long AsI64()
    {
        EnsureKind(WasmValueKind.I64);
        return unchecked((long)low64_);
    }

    /// <summary>
    /// 32ビット浮動小数点数からビット列を保持してvalueを構築する
    /// </summary>
    /// <param name="value">保持する32ビット浮動小数点数</param>
    /// <returns>NaNや符号付きゼロを含むビット列を保持するf32のvalue</returns>
    public static WasmValue FromF32(float value)
    {
        return FromF32Bits(BitConverter.SingleToUInt32Bits(value));
    }

    /// <summary>
    /// ビット列を指定して32ビット浮動小数点数のvalueを構築する
    /// </summary>
    /// <param name="bits">f32として保持する32ビット列</param>
    /// <returns>指定したビット列をそのまま保持するf32のvalue</returns>
    public static WasmValue FromF32Bits(uint bits)
    {
        return new WasmValue(WasmValueKind.F32, bits, 0, null);
    }

    /// <summary>
    /// 32ビット浮動小数点数を取得する
    /// </summary>
    /// <returns>保持するビット列を32ビット浮動小数点数として解釈した値</returns>
    /// <exception cref="InvalidOperationException">valueの型がf32ではない場合</exception>
    public float AsF32()
    {
        return BitConverter.UInt32BitsToSingle(AsF32Bits());
    }

    /// <summary>
    /// 32ビット浮動小数点数のビット列を取得する
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する、保持中の32ビット列</returns>
    /// <exception cref="InvalidOperationException">valueの型がf32ではない場合</exception>
    public uint AsF32Bits()
    {
        EnsureKind(WasmValueKind.F32);
        return (uint)low64_;
    }

    /// <summary>
    /// 64ビット浮動小数点数からビット列を保持してvalueを構築する
    /// </summary>
    /// <param name="value">保持する64ビット浮動小数点数</param>
    /// <returns>NaNや符号付きゼロを含むビット列を保持するf64のvalue</returns>
    public static WasmValue FromF64(double value)
    {
        return FromF64Bits(BitConverter.DoubleToUInt64Bits(value));
    }

    /// <summary>
    /// ビット列を指定して64ビット浮動小数点数のvalueを構築する
    /// </summary>
    /// <param name="bits">f64として保持する64ビット列</param>
    /// <returns>指定したビット列をそのまま保持するf64のvalue</returns>
    public static WasmValue FromF64Bits(ulong bits)
    {
        return new WasmValue(WasmValueKind.F64, bits, 0, null);
    }

    /// <summary>
    /// 64ビット浮動小数点数を取得する
    /// </summary>
    /// <returns>保持するビット列を64ビット浮動小数点数として解釈した値</returns>
    /// <exception cref="InvalidOperationException">valueの型がf64ではない場合</exception>
    public double AsF64()
    {
        return BitConverter.UInt64BitsToDouble(AsF64Bits());
    }

    /// <summary>
    /// 64ビット浮動小数点数のビット列を取得する
    /// </summary>
    /// <returns>NaNや符号付きゼロも区別する、保持中の64ビット列</returns>
    /// <exception cref="InvalidOperationException">valueの型がf64ではない場合</exception>
    public ulong AsF64Bits()
    {
        EnsureKind(WasmValueKind.F64);
        return low64_;
    }

    /// <summary>
    /// 上下64ビットを指定して128ビットベクトルのvalueを構築する
    /// </summary>
    /// <param name="low64">ベクトルの下位64ビット</param>
    /// <param name="high64">ベクトルの上位64ビット</param>
    /// <returns>指定した128ビット列を保持するv128のvalue</returns>
    public static WasmValue FromV128(ulong low64, ulong high64)
    {
        return new WasmValue(WasmValueKind.V128, low64, high64, null);
    }

    /// <summary>
    /// 128ビットベクトルの上下64ビットを取得する
    /// </summary>
    /// <returns>下位64ビットと上位64ビットの組</returns>
    /// <exception cref="InvalidOperationException">valueの型がv128ではない場合</exception>
    public (ulong Low64, ulong High64) AsV128()
    {
        EnsureKind(WasmValueKind.V128);
        return (low64_, high64_);
    }

    /// <summary>
    /// 関数への参照またはnullからfuncrefのvalueを構築する
    /// </summary>
    /// <param name="value">保持する関数への参照またはnull</param>
    /// <returns>元の関数と同じ参照先を保持するfuncrefのvalue nullもfuncrefとして保持する</returns>
    public static WasmValue FromFuncRef(WasmFunction? value)
    {
        return new WasmValue(WasmValueKind.FuncRef, 0, 0, value);
    }

    /// <summary>
    /// funcrefの参照先またはnullを取得する
    /// </summary>
    /// <returns>構築時に保持した関数への参照またはnull</returns>
    /// <exception cref="InvalidOperationException">valueの型がfuncrefではない場合</exception>
    public WasmFunction? AsFuncRef()
    {
        EnsureKind(WasmValueKind.FuncRef);
        return (WasmFunction?)reference_;
    }

    /// <summary>
    /// CLRオブジェクトへの参照またはnullからexternrefのvalueを構築する
    /// </summary>
    /// <param name="value">保持するCLRオブジェクトへの参照またはnull</param>
    /// <returns>元のオブジェクトと同じ参照先を保持するexternrefのvalue nullもexternrefとして保持する</returns>
    public static WasmValue FromExternRef(object? value)
    {
        return new WasmValue(WasmValueKind.ExternRef, 0, 0, value);
    }

    /// <summary>
    /// externrefの元のCLRオブジェクトへの参照またはnullを取得する
    /// </summary>
    /// <returns>構築時に保持したCLRオブジェクトへの参照またはnull</returns>
    /// <exception cref="InvalidOperationException">valueの型がexternrefではない場合</exception>
    public object? AsExternRef()
    {
        EnsureKind(WasmValueKind.ExternRef);
        return reference_;
    }

    /// <summary>
    /// valueを取得する操作が、保持中の型に対応することを確認する
    /// </summary>
    /// <param name="expected">取得する操作が要求するvalueの型</param>
    /// <exception cref="InvalidOperationException">保持中の型が要求された型と異なる場合</exception>
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
