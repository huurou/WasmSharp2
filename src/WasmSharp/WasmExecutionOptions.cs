namespace WasmSharp;

/// <summary>
/// 実行時のオプション設定
/// </summary>
public sealed record WasmExecutionOptions
{
    /// <summary>
    /// 既定の最大呼び出し深さ1024を持つ実行ポリシー
    /// </summary>
    public static WasmExecutionOptions Default { get; } = new(1024);

    /// <summary>
    /// 関数の呼び出し深さの上限
    /// </summary>
    public int MaxCallDepth { get; }

    /// <summary>
    /// 正の最大呼び出し深さを指定して構築する
    /// </summary>
    /// <param name="maxCallDepth">関数の呼び出し深さの上限</param>
    public WasmExecutionOptions(int maxCallDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallDepth);
        MaxCallDepth = maxCallDepth;
    }
}
