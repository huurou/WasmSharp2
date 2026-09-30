namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// module識別子や登録名が指す成功実体、または成立しなかったcommandによる利用不能状態
/// </summary>
/// <typeparam name="T">成功実体の型</typeparam>
internal sealed record ScriptBinding<T>
    where T : class
{
    /// <summary>
    /// 成功実体 利用不能状態ではnull
    /// </summary>
    internal T? Value { get; }

    /// <summary>
    /// 利用不能の原因 成功実体ではnull
    /// </summary>
    internal UnavailableCause? Cause { get; }

    /// <summary>
    /// 成功実体または利用不能の原因を保持する名前の対応を作る。
    /// </summary>
    /// <param name="value">成功実体 利用不能状態を作る場合はnull</param>
    /// <param name="cause">利用不能の原因 成功状態を作る場合はnull</param>
    private ScriptBinding(T? value, UnavailableCause? cause)
    {
        Value = value;
        Cause = cause;
    }

    /// <summary>
    /// 成功実体を指す状態を作る。
    /// </summary>
    /// <param name="value">名前の参照先として保持する成功実体 コピーせず同じ実体を保持する</param>
    /// <returns>成功実体を持ち、利用不能の原因を持たない名前の対応</returns>
    internal static ScriptBinding<T> Available(T value)
    {
        return new(value, null);
    }

    /// <summary>
    /// 原因付きの利用不能状態を作る。
    /// </summary>
    /// <param name="cause">参照先を利用不能にしたcommandと元の失敗</param>
    /// <returns>成功実体を持たず、指定した原因を持つ名前の対応</returns>
    internal static ScriptBinding<T> Unavailable(UnavailableCause cause)
    {
        return new(null, cause);
    }
}
