using WasmSharp.Execution;

namespace WasmSharp;

/// <summary>
/// 同期呼び出しの連鎖で共有する実行状態を所有する
/// </summary>
/// <remarks>
/// InvokeとInstantiateへ明示的に渡し、ホストから再入する場合も受け取った同じオブジェクトを使う。
/// 最外側の定義関数またはstartが開始すると、そのinstanceのポリシーで深さと上限を共有する。
/// 最外側の実行が終了すると内部状態を解放するため、次の独立した呼び出しにも再利用できる。
/// 同じオブジェクトを使った並行実行には対応しない。
/// </remarks>
public sealed class WasmExecutionContext
{
    /// <summary>
    /// 進行中のWasm実行の状態 実行中でなければnull
    /// </summary>
    internal InterpreterContext? Current { get; private set; }

    /// <summary>
    /// 進行中のWasm実行を持たないコンテキストを構築する
    /// </summary>
    public WasmExecutionContext() { }

    /// <summary>
    /// 進行中の状態を再利用し、存在しなければ指定ポリシーで開始する
    /// </summary>
    /// <param name="options">新しく開始する場合に適用する実行ポリシー</param>
    /// <param name="isOutermost">この呼び出しで新しく開始した場合はtrue</param>
    /// <returns>同期呼び出しの間で共有する実行状態</returns>
    internal InterpreterContext Enter(WasmExecutionOptions options, out bool isOutermost)
    {
        isOutermost = Current is null;
        return Current ??= new InterpreterContext(this, options);
    }

    /// <summary>
    /// 最外側の呼び出しが終了した場合に実行状態を解放する
    /// </summary>
    /// <param name="isOutermost">対応するEnterで取得した値</param>
    internal void Exit(bool isOutermost)
    {
        if (isOutermost)
        {
            Current = null;
        }
    }
}
