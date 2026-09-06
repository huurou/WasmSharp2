namespace WasmSharp;

/// <summary>
/// Wasmモジュール 静的なモジュール定義
/// </summary>
public sealed class WasmModule
{
    /// <summary>
    /// 入力バイト列をWasmモジュールにデコードします。
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>デコードされたモジュール</returns>
    public static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 入力ストリームをWasmモジュールにデコードします。
    /// </summary>
    /// <param name="stream">入力ストリーム</param>
    /// <returns>デコードされたモジュール</returns>
    public static WasmModule Decode(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("入力ストリームが読み取り不可でした。", nameof(stream));
        }

        throw new NotImplementedException();
    }

    public WasmModule Validate()
    {
        throw new NotImplementedException();
    }

    public WasmInstance Instantiate(
        ReadOnlySpan<WasmHostModule> hostModules,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
