namespace WasmSharp;

/// <summary>
/// instanceが新しいWasm実行コンテキストを開くときの実行ポリシー
/// </summary>
/// <remarks>
/// 定義関数への入口では所属instance、startの入口ではstartを持つinstanceのポリシーを使う。
/// 同じコンテキストでは最外側の上限を維持し、別instanceへの呼び出しやホストからの同期再入でも切り替えない。
/// コンテキスト外でホスト関数を直接呼び出す場合、instanceを指定しただけではこのポリシーを適用しない。
/// </remarks>
public sealed record WasmExecutionOptions
{
    /// <summary>
    /// 既定の最大呼び出し深さ1024を持つ実行ポリシー
    /// </summary>
    public static WasmExecutionOptions Default { get; } = new(1024);

    /// <summary>
    /// 関数の呼び出し深さの上限
    /// </summary>
    /// <remarks>
    /// 最初の関数を深さ1として数え、同じコンテキスト内のホスト関数と同期再入も深さを共有する。
    /// 終了した呼び出しの深さは解放する。任意のホストコード自身のCLR再帰を制限する値ではない。
    /// </remarks>
    public int MaxCallDepth { get; }

    /// <summary>
    /// 正の最大呼び出し深さを指定して構築する
    /// </summary>
    /// <param name="maxCallDepth">同じ実行コンテキストで共有する、正の呼び出し深さの上限</param>
    /// <exception cref="ArgumentOutOfRangeException">maxCallDepthが0以下の場合</exception>
    public WasmExecutionOptions(int maxCallDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallDepth);
        MaxCallDepth = maxCallDepth;
    }
}
