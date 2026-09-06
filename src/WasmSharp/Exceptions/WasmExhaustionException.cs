namespace WasmSharp.Exceptions;

/// <summary>
/// ランタイムが管理する実行資源の上限に達したことを示す例外
/// </summary>
/// <param name="message">例外の原因を説明するメッセージ</param>
/// <param name="reason">上限に達した実行資源の原因</param>
/// <param name="limit">この実行に適用された上限</param>
/// <param name="location">失敗した処理段階と入力上の位置</param>
/// <param name="innerException">原因となった例外</param>
public class WasmExhaustionException(
    string? message,
    WasmExhaustionReason reason,
    int limit,
    WasmFailureLocation location,
    Exception? innerException = null
) : WasmException(message, location, innerException)
{
    /// <summary>
    /// 上限に達した実行資源の原因
    /// </summary>
    public WasmExhaustionReason Reason { get; } = reason;

    /// <summary>
    /// この実行に適用された上限
    /// </summary>
    public int Limit { get; } = limit;
}

/// <summary>
/// ランタイムが管理する実行資源の上限到達の原因
/// </summary>
public enum WasmExhaustionReason
{
    /// <summary>
    /// 関数の呼び出し深さの上限
    /// </summary>
    CallDepthLimit,
}
