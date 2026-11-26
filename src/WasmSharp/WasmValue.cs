namespace WasmSharp;

/// <summary>
/// Wasmの型付きvalueを保持する値型
/// </summary>
public readonly struct WasmValue
{
    /// <summary>
    /// Wasmの内部参照をホストへ渡し、戻されたときに同じ内部参照を取り出すための参照先
    /// </summary>
    private sealed record ExternalizedReference(WasmValue Source)
    {
        // 再び外部化したとき、ホストが渡した同じオブジェクトを返せるようにする。
        internal WasmValue InternalValue => new WasmValue(Source.Kind, Source._low64, Source._high64, this);
    }

    /// <summary>
    /// 数値スカラーやi31のビット列、またはv128の下位64ビット
    /// </summary>
    private readonly ulong _low64;

    /// <summary>
    /// v128の上位64ビット
    /// </summary>
    private readonly ulong _high64;

    /// <summary>
    /// 参照先、または内部参照へ戻す前の外部参照を保持する
    /// </summary>
    private readonly object? _reference;

    // i31を再び外部化するための参照先は、値の等しさやハッシュには含めない。
    private (WasmValueKind Kind, ulong Low64, ulong High64, object? Reference) EqualityFields =>
        (Kind, _low64, _high64, Kind == WasmValueKind.I31Ref ? null : _reference);

    /// <summary>
    /// 格納された値の実行時の種類
    /// </summary>
    /// <remarks>
    /// 例えばeqrefの宣言に渡したi31の種類は<see cref="WasmValueKind.I31Ref"/>、
    /// 同じ宣言に渡したGC参照のnullの種類は<see cref="WasmValueKind.AnyRef"/>となる。
    /// </remarks>
    public WasmValueKind Kind { get; }

    /// <summary>
    /// 値がnull参照かどうか GC参照のnullはどの宣言型から生成してもanyの階層に属する
    /// </summary>
    public bool IsNullReference =>
        _reference is null && Kind is WasmValueKind.FuncRef or WasmValueKind.ExternRef or WasmValueKind.AnyRef;

    /// <summary>
    /// valueの型と対応するビット列または参照先を保持する
    /// </summary>
    /// <param name="kind">保持するvalueの型</param>
    /// <param name="low64">数値スカラーやi31のビット列、またはv128の下位64ビット</param>
    /// <param name="high64">v128の上位64ビット 他の型では0</param>
    /// <param name="reference">参照型の参照先またはnull 数値型ではnull</param>
    private WasmValue(WasmValueKind kind, ulong low64, ulong high64, object? reference)
    {
        Kind = kind;
        _low64 = low64;
        _high64 = high64;
        _reference = reference;
    }

    /// <summary>
    /// 数値とv128ではゼロを、参照ではその型の階層に属するnullを返す
    /// </summary>
    internal static WasmValue CreateInitialValue(WasmValueKind kind)
    {
        if (!kind.IsSupported())
        {
            throw new InvalidOperationException("値型が不正です。");
        }
        return new WasmValue(kind.GetTopKind(), 0, 0, null);
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
        return unchecked((int)_low64);
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
        return unchecked((long)_low64);
    }

    /// <summary>
    /// i32またはi64のビット列を符号なし整数として取得する
    /// </summary>
    /// <returns>i32はゼロ拡張した値、i64は保持する64ビット列</returns>
    /// <exception cref="InvalidOperationException">valueの型がi32またはi64以外の場合</exception>
    internal ulong AsUnsignedInteger()
    {
        if (Kind is not (WasmValueKind.I32 or WasmValueKind.I64))
        {
            throw new InvalidOperationException($"{Kind}のvalueを整数として取得できません。");
        }
        return _low64;
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
        return (uint)_low64;
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
        return _low64;
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
        return (_low64, _high64);
    }

    /// <summary>
    /// 指定した参照型と同じ階層のnullを構築する
    /// </summary>
    /// <param name="kind">nullを構築する参照型の種類</param>
    /// <returns>指定した階層の最上位型が値の種類となる。例えばi31refのnullの種類はanyrefとなる</returns>
    /// <exception cref="ArgumentOutOfRangeException">kindが対応する参照型でない場合</exception>
    public static WasmValue FromNullReference(WasmValueKind kind)
    {
        if (!kind.IsReference())
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "参照型を指定してください。");
        }
        return new WasmValue(kind.GetTopKind(), 0, 0, null);
    }

    /// <summary>
    /// 32ビット整数の下位31ビットを保持するi31参照を構築する
    /// </summary>
    /// <param name="value">下位31ビットを取り出す整数</param>
    /// <returns>指定したビット列を保持する非nullのi31参照</returns>
    public static WasmValue FromI31(int value)
    {
        return new WasmValue(WasmValueKind.I31Ref, (uint)(value & 0x7FFF_FFFF), 0, null);
    }

    /// <summary>
    /// i31参照が保持する31ビットを符号拡張して取得する
    /// </summary>
    /// <returns>保持する31ビットを符号拡張した32ビット整数</returns>
    /// <exception cref="InvalidOperationException">値が非nullのi31参照でない場合</exception>
    public int AsI31Signed()
    {
        return (AsI31Unsigned() << 1) >> 1;
    }

    /// <summary>
    /// i31参照が保持する31ビットをゼロ拡張して取得する
    /// </summary>
    /// <returns>保持する31ビットをゼロ拡張した32ビット整数</returns>
    /// <exception cref="InvalidOperationException">値が非nullのi31参照でない場合</exception>
    public int AsI31Unsigned()
    {
        EnsureKind(WasmValueKind.I31Ref);
        return (int)_low64;
    }

    /// <summary>
    /// 生成した構造体への参照を保持する
    /// </summary>
    internal static WasmValue FromStruct(WasmStruct value)
    {
        return new WasmValue(WasmValueKind.StructRef, 0, 0, value);
    }

    /// <summary>
    /// 参照先の構造体を取得する
    /// </summary>
    internal WasmStruct AsStruct()
    {
        EnsureKind(WasmValueKind.StructRef);
        return (WasmStruct)_reference!;
    }

    /// <summary>
    /// 構造体の参照先を不透明なCLRオブジェクトとして取得する
    /// </summary>
    /// <returns>同じ構造体を外部化したときにも得られる参照先</returns>
    /// <exception cref="InvalidOperationException">値が非nullの構造体参照でない場合</exception>
    public object AsStructRef()
    {
        return AsStruct();
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
        return (WasmFunction?)_reference;
    }

    /// <summary>
    /// CLRオブジェクトへの参照またはnullから、anyの階層に属するホスト参照を構築する
    /// </summary>
    /// <param name="value">ホストが保持するオブジェクトへの参照またはnull</param>
    /// <returns>元の参照先を保持するanyref</returns>
    public static WasmValue FromHostRef(object? value)
    {
        var host = new WasmValue(WasmValueKind.AnyRef, 0, 0, value);
        // 外部化したWasm参照のオブジェクトも、ホスト参照として受け取ったときは区別して保持する。
        return value is ExternalizedReference or WasmStruct ? new ExternalizedReference(host).InternalValue : host;
    }

    /// <summary>
    /// 内部のホスト参照が保持するCLRオブジェクトまたはnullを取得する
    /// </summary>
    /// <returns>ホストが保持するオブジェクトへの参照またはnull</returns>
    /// <exception cref="InvalidOperationException">valueがanyのホスト参照またはnull以外の場合</exception>
    public object? AsHostRef()
    {
        EnsureKind(WasmValueKind.AnyRef);
        return _reference is ExternalizedReference wrapped ? wrapped.Source._reference : _reference;
    }

    internal WasmValue InternalizeExtern()
    {
        return AsExternRef() switch
        {
            WasmStruct structure => FromStruct(structure),
            ExternalizedReference wrapped => wrapped.InternalValue,
            var host => FromHostRef(host),
        };
    }

    internal WasmValue ExternalizeAny()
    {
        // 構造体は、繰り返し外部化しても同じ実体をホストに渡す。
        if (Kind is WasmValueKind.AnyRef or WasmValueKind.StructRef || _reference is ExternalizedReference)
        {
            return FromExternRef(_reference);
        }
        return FromExternRef(new ExternalizedReference(this));
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
    /// <returns>CLRオブジェクトへの参照またはnull 外部化したWasm参照のオブジェクトはFromExternRefへ渡して戻せる</returns>
    /// <exception cref="InvalidOperationException">valueの型がexternrefではない場合</exception>
    public object? AsExternRef()
    {
        EnsureKind(WasmValueKind.ExternRef);
        return _reference;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// i31参照は外部参照との変換を経た後も、保持する31ビットで等しさを判定する。
    /// 構造体参照は同じ実体を指すときに等しく、フィールドの更新は等しさに影響しない。
    /// 外部参照の等しさは、ホストオブジェクトのEqualsに従う。
    /// </remarks>
    public override bool Equals(object? obj)
    {
        return obj is WasmValue other && HasSameValue(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return EqualityFields.GetHashCode();
    }

    /// <summary>
    /// <see cref="Equals(object)"/>と同じ基準で2つの値が等しいかを返す
    /// </summary>
    public static bool operator ==(WasmValue left, WasmValue right) => left.HasSameValue(right);

    /// <summary>
    /// <see cref="Equals(object)"/>と同じ基準で2つの値が異なるかを返す
    /// </summary>
    public static bool operator !=(WasmValue left, WasmValue right) => !left.HasSameValue(right);

    private bool HasSameValue(WasmValue other)
    {
        return EqualityFields.Equals(other.EqualityFields);
    }

    /// <summary>
    /// valueを取得する操作が、保持中の型に対応することを確認する
    /// </summary>
    /// <param name="expected">取得する操作が要求するvalueの型</param>
    /// <exception cref="InvalidOperationException">保持中の型が要求された型と異なる場合</exception>
    private void EnsureKind(WasmValueKind expected)
    {
        if (Kind != expected)
        {
            throw new InvalidOperationException($"{Kind}のvalueを{expected}として取得できません。");
        }
    }
}
