namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供する関数を保持する
/// </summary>
/// <param name="value">提供登録とimport先で共有する関数の実体</param>
internal sealed class FunctionExternalValue(WasmFunction value) : ExternalValue
{
    /// <summary>
    /// 提供する関数
    /// </summary>
    internal WasmFunction Value { get; } = value;
}
