namespace WasmSharp.Exceptions;

/// <summary>
/// Wasmの処理段階
/// </summary>
public enum WasmProcessingStage
{
    /// <summary>
    /// バイナリの構文を解釈する段階
    /// </summary>
    Decode,

    /// <summary>
    /// 型と構造の規則を検証する段階
    /// </summary>
    Validate,

    /// <summary>
    /// リンクと初期化を行う段階
    /// </summary>
    Instantiate,

    /// <summary>
    /// 関数を呼び出す段階
    /// </summary>
    Invoke,
}
