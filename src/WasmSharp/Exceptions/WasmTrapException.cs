namespace WasmSharp.Exceptions;

/// <summary>
/// 実行中のtrap時例外
/// </summary>
public class WasmTrapException : WasmException
{
    /// <summary>
    /// Wasmの実行規則で定められたtrapの原因。診断情報がない場合はnull
    /// </summary>
    public WasmTrapReason? Reason { get; }

    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmTrapException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmTrapException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmTrapException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// trapの原因と発生位置を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="reason">Wasmの実行規則で定められたtrapの原因</param>
    /// <param name="location">失敗した処理段階と入力上の位置</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmTrapException(
        string? message,
        WasmTrapReason reason,
        WasmFailureLocation location,
        Exception? innerException = null
    )
        : base(message, location, innerException)
    {
        Reason = reason;
    }
}
