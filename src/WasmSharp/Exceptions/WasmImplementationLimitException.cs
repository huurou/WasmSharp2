namespace WasmSharp.Exceptions;

/// <summary>
/// 入力またはコレクションを実装上の上限により保持できないことを示す例外
/// </summary>
/// <param name="message">例外の原因を説明するメッセージ</param>
/// <param name="reason">保持できない入力またはコレクションの原因</param>
/// <param name="limit">保持できるサイズの上限</param>
/// <param name="location">失敗した処理段階と入力上の位置</param>
/// <param name="innerException">原因となった例外</param>
public class WasmImplementationLimitException(
    string? message,
    WasmImplementationLimitReason reason,
    int limit,
    WasmFailureLocation location,
    Exception? innerException = null
) : WasmException(message, location, innerException)
{
    /// <summary>
    /// 保持できない入力またはコレクションの原因
    /// </summary>
    public WasmImplementationLimitReason Reason { get; } = reason;

    /// <summary>
    /// 保持できるサイズの上限
    /// </summary>
    public int Limit { get; } = limit;
}

/// <summary>
/// 入力またはコレクションの保持上限の原因
/// </summary>
public enum WasmImplementationLimitReason
{
    /// <summary>
    /// 入力バイナリのサイズ上限
    /// </summary>
    InputSize,

    /// <summary>
    /// コレクションの要素数上限
    /// </summary>
    CollectionSize,
}
