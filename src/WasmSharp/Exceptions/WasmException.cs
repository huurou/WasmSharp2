namespace WasmSharp.Exceptions;

/// <summary>
/// WasmSharp例外基底クラス
/// </summary>
public abstract class WasmException : Exception
{
    /// <summary>
    /// 失敗した処理段階と入力上の位置。診断情報がない場合はnull
    /// </summary>
    public WasmFailureLocation? Location { get; }

    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// メッセージ、発生位置、原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="location">失敗した処理段階と入力上の位置</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmException(string? message, WasmFailureLocation location, Exception? innerException)
        : base(message, innerException)
    {
        Location = location;
    }
}
