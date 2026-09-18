using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// functionを表現するクラス
/// </summary>
public sealed class WasmFunction
{
    private readonly WasmInstance? instance_;
    private readonly WasmFunctionType? hostType_;
    private readonly uint definitionIndex_;

    /// <summary>
    /// instanceに所属しないホスト関数かどうか
    /// </summary>
    internal bool IsHost => instance_ is null;

    /// <summary>
    /// instanceを受け取らない形式のcallback。別形式または定義関数ではnull
    /// </summary>
    internal WasmHostCallback? HostCallback { get; }

    /// <summary>
    /// instanceを受け取る形式のcallback。別形式または定義関数ではnull
    /// </summary>
    internal WasmHostInstanceCallback? HostInstanceCallback { get; }

    /// <summary>
    /// この関数が所属するinstance
    /// </summary>
    internal WasmInstance Instance =>
        instance_ ?? throw new InvalidOperationException("ホスト関数はinstanceに所属しません。");

    /// <summary>
    /// 所属するmoduleの関数index空間における位置
    /// </summary>
    internal uint FunctionIndex { get; }

    /// <summary>
    /// 所属するmoduleが保持するデコード済み関数定義
    /// </summary>
    internal DecodedFunction Definition => Instance.Module.Functions[(int)definitionIndex_];

    /// <summary>
    /// 所属するmoduleが定義配列の添字に保持する実行コード
    /// </summary>
    internal FunctionCode Code => Instance.Module.FunctionCodes[(int)definitionIndex_];

    /// <summary>
    /// functionの型
    /// </summary>
    public WasmFunctionType Type => hostType_ ?? Instance.Module.Types[(int)Definition.TypeIndex];

    /// <summary>
    /// 関数indexと定義配列の添字が一致する場合に、instanceと関数の実体を結び付ける
    /// </summary>
    /// <param name="instance">この関数が所属するinstance</param>
    /// <param name="functionIndex">定義配列の添字と同じmodule全体の関数index</param>
    internal WasmFunction(WasmInstance instance, uint functionIndex)
        : this(instance, functionIndex, functionIndex) { }

    /// <summary>
    /// module全体の関数indexと定義配列の添字を区別して関数の実体を構築する
    /// </summary>
    /// <param name="instance">この定義関数が所属するinstance</param>
    /// <param name="functionIndex">importを含むmodule全体の関数index</param>
    /// <param name="definitionIndex">module内の定義配列の添字</param>
    internal WasmFunction(WasmInstance instance, uint functionIndex, uint definitionIndex)
    {
        instance_ = instance;
        FunctionIndex = functionIndex;
        definitionIndex_ = definitionIndex;
    }

    private WasmFunction(
        WasmFunctionType type,
        WasmHostCallback? callback,
        WasmHostInstanceCallback? instanceCallback
    )
    {
        hostType_ = type;
        HostCallback = callback;
        HostInstanceCallback = instanceCallback;
    }

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
        return new WasmFunction(type, callback, null);
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
        return new WasmFunction(type, null, callback);
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

        if (IsHost)
        {
            throw new WasmUnsupportedFeatureException("ホスト関数の呼び出しは未対応です。");
        }

        return ExecutionBoundary.Invoke(this, arguments, WasmProcessingStage.Invoke);
    }
}
