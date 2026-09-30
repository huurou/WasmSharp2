namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供する関数やリソースを表す基底型
/// </summary>
internal abstract class ExternalValue
{
    /// <summary>
    /// 同一アセンブリ内の派生型に限定して、提供する外部要素を構築する
    /// </summary>
    private protected ExternalValue() { }
}
