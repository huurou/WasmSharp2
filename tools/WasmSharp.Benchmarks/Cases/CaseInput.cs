namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 公開Invokeまたは準備段階の後検査で使う関数・引数と、計時外で準備する配列
/// </summary>
internal sealed record CaseInput(
    string FunctionExport,
    List<NumericValue> Arguments,
    List<ArrayInput> Arrays
);

/// <summary>
/// memory内へ配置する固定入力配列 Offsetはバイト位置、Lengthは要素数でいずれも符号なし32ビット整数
/// </summary>
/// <param name="Seed">SeededU32の固定seed 他の方式ではnull</param>
/// <param name="FillByte">RepeatedByteの固定バイト 他の方式ではnull</param>
internal sealed record ArrayInput(
    string MemoryExport,
    uint Offset,
    ArrayElementType ElementType,
    uint Length,
    ArrayInputPattern Pattern,
    uint? Seed,
    byte? FillByte
);

/// <summary>
/// 固定ケースの配列要素型 複数バイトの要素はlittle-endianで配置する。
/// </summary>
internal enum ArrayElementType
{
    U8,
    U32,
}

/// <summary>
/// 固定ケースの入力生成方式 生成処理はInputFactoryが所有する。
/// </summary>
internal enum ArrayInputPattern
{
    RepeatedByte,
    IndexModulo256,
    SeededU32,
}
