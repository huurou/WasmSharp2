namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// module識別子や登録名が指す成功実体、または成立しなかったcommandによる利用不能状態
/// </summary>
/// <typeparam name="T">成功実体の型</typeparam>
internal sealed record ScriptBinding<T>
    where T : class
{
    /// <summary>
    /// 成功実体。利用不能状態ではnull
    /// </summary>
    internal T? Value { get; }

    /// <summary>
    /// 利用不能の原因。成功実体ではnull
    /// </summary>
    internal UnavailableCause? Cause { get; }

    private ScriptBinding(T? value, UnavailableCause? cause)
    {
        Value = value;
        Cause = cause;
    }

    /// <summary>
    /// 成功実体を指す状態を作る。
    /// </summary>
    internal static ScriptBinding<T> Available(T value)
    {
        return new(value, null);
    }

    /// <summary>
    /// 原因付きの利用不能状態を作る。
    /// </summary>
    internal static ScriptBinding<T> Unavailable(UnavailableCause cause)
    {
        return new(null, cause);
    }
}
