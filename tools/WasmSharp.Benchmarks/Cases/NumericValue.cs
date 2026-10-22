namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 数値の型とbit列を保持する保存用定義 i32/f32は8桁、i64/f64は16桁の小文字16進表記
/// </summary>
internal sealed record NumericValue(NumericType Type, string Bits);

/// <summary>
/// 固定ケースの引数・期待結果で扱うWasm数値型 整数は符号解釈を加えずbit列として保持する。
/// </summary>
internal enum NumericType
{
    I32,
    I64,
    F32,
    F64,
}
