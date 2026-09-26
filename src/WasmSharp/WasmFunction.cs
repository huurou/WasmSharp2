using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp;

/// <summary>
/// 定義関数とホスト関数に共通する型と呼び出し操作
/// </summary>
public abstract class WasmFunction
{
    /// <summary>
    /// functionの型
    /// </summary>
    public abstract WasmFunctionType Type { get; }

    /// <summary>
    /// ライブラリ内部の具体型だけが構築できる関数の共通部分
    /// </summary>
    private protected WasmFunction() { }

    /// <summary>
    /// 明示関数型とinstanceを受け取らないcallbackからホスト関数を生成する
    /// </summary>
    /// <param name="type">Wasmの引数と結果の型</param>
    /// <param name="callback">所有済みの結果を返す処理</param>
    /// <returns>instanceに所属しないホスト関数</returns>
    public static WasmFunction CreateHost(WasmFunctionType type, WasmHostCallback callback)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(callback);
        return new HostFunction(type, callback);
    }

    /// <summary>
    /// 明示関数型と呼び出し時のinstanceを受け取るcallbackからホスト関数を生成する
    /// </summary>
    /// <param name="type">instanceを含めないWasmの引数と結果の型</param>
    /// <param name="callback">所有済みの結果を返す処理</param>
    /// <returns>instanceに所属しないホスト関数</returns>
    public static WasmFunction CreateHost(WasmFunctionType type, WasmHostInstanceCallback callback)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(callback);
        return new InstanceHostFunction(type, callback);
    }

    /// <summary>
    /// 指定した引数で関数を呼び出す
    /// </summary>
    /// <param name="arguments">関数に渡すvalueのコレクション</param>
    /// <returns>関数が返すvalueのコレクション</returns>
    public WasmResults Invoke(ReadOnlySpan<WasmValue> arguments)
    {
        var parameters = Type.Parameters;
        if (arguments.Length != parameters.Length)
        {
            throw new ArgumentException("引数の個数が関数型と一致しません。", nameof(arguments));
        }

        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Kind != parameters[i])
            {
                throw new ArgumentException("引数の型が関数型と一致しません。", nameof(arguments));
            }
        }

        return ExecutionBoundary.Invoke(this, arguments, WasmProcessingStage.Invoke);
    }
}
