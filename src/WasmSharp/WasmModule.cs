namespace WasmSharp;

/// <summary>
/// 静的なmodule定義を表現するクラス
/// </summary>
public sealed class WasmModule
{
    /// <summary>
    /// 入力バイト列をmoduleにデコードする
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>デコードされたモジュール</returns>
    public static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 入力ストリームをmoduleにデコードする
    /// </summary>
    /// <param name="stream">入力ストリーム</param>
    /// <returns>デコードされたmodule</returns>
    public static WasmModule Decode(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("入力ストリームが読み取り不可でした。", nameof(stream));
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// moduleがWasmの型規則と構造規則を満たすか検証する
    /// </summary>
    /// <returns>検証済みのmodulle</returns>
    public WasmModule Validate()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// host moduleでimportを解決しmoduleをインスタンス化する
    /// </summary>
    /// <param name="hostModules">importする関数やリソースを提供するhost moduleのリスト</param>
    /// <param name="options">実行時のオプション設定</param>
    /// <returns>instance</returns>
    public WasmInstance Instantiate(
        ReadOnlySpan<WasmHostModule> hostModules,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
