namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// f32・f64の期待値、またはf32・f64のlaneの期待値に使うNaN pattern
/// </summary>
internal enum NanPattern
{
    /// <summary>
    /// 符号を除いたビット列がその型のcanonical NaNに一致するNaN
    /// </summary>
    Canonical,

    /// <summary>
    /// 指数部がすべて1かつ仮数部の最上位ビットが1のNaN
    /// </summary>
    Arithmetic,
}
