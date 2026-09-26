using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp;

/// <summary>
/// 定義関数とホスト関数に共通する型と呼び出し操作
/// </summary>
/// <remarks>定義関数は元のinstanceに所属し、ホスト関数はinstanceに所属せず同じ実体を共有する</remarks>
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
    /// <exception cref="ArgumentNullException">typeまたはcallbackがnullの場合</exception>
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
    /// <exception cref="ArgumentNullException">typeまたはcallbackがnullの場合</exception>
    public static WasmFunction CreateHost(WasmFunctionType type, WasmHostInstanceCallback callback)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(callback);
        return new InstanceHostFunction(type, callback);
    }

    /// <summary>
    /// instanceを指定せずに、定義関数またはinstanceを受け取らないホスト関数を呼び出す
    /// </summary>
    /// <remarks>
    /// 定義関数は元の所属instanceで実行する。instance必須のホスト関数では、取得元や進行中の呼び出し元でinstanceを補わない。
    /// ホスト処理が投げた例外は、型と実体を変えずに伝播する。
    /// </remarks>
    /// <param name="arguments">関数型の引数列と個数・型・順序が一致する値</param>
    /// <returns>後続の呼び出しに影響されない、宣言された順序の結果</returns>
    /// <exception cref="ArgumentException">引数の個数または型が関数型と一致しない場合</exception>
    /// <exception cref="ArgumentNullException">instance必須のホスト関数を呼び出した場合</exception>
    /// <exception cref="InvalidOperationException">ホスト処理の結果がnull、または宣言された個数・型と一致しない場合</exception>
    /// <exception cref="WasmTrapException">Wasmの実行規則によるtrapが発生した場合</exception>
    /// <exception cref="WasmExhaustionException">呼び出し深さが上限に達したか、CLRスタックの余裕が不足した場合</exception>
    /// <exception cref="WasmImplementationLimitException">実行に必要なフレーム数または値の数が保持上限を超える場合</exception>
    public WasmResults Invoke(ReadOnlySpan<WasmValue> arguments)
    {
        return Invoke(null!, arguments);
    }

    /// <summary>
    /// ホスト処理のアクセス先instanceと値引数を指定して関数を呼び出す
    /// </summary>
    /// <remarks>
    /// 定義関数はinstance引数にかかわらず元の所属instanceで実行し、新しい実行コンテキストの上限も所属instanceから選ぶ。
    /// ホスト関数へのinstance指定だけでは実行コンテキストを開始せず、進行中のWasm実行への同期再入では既存の深さと上限を共有する。
    /// ホスト処理が投げた例外は、型と実体を変えずに伝播する。
    /// </remarks>
    /// <param name="instance">instance必須のホスト処理へ渡す対象。定義関数とinstanceを受け取らないホスト関数では、nullを含め無視する</param>
    /// <param name="arguments">関数型の引数列と個数・型・順序が一致する値</param>
    /// <returns>後続の呼び出しに影響されない、宣言された順序の結果</returns>
    /// <exception cref="ArgumentException">引数の個数または型が関数型と一致しない場合</exception>
    /// <exception cref="ArgumentNullException">instance必須のホスト関数にnullを指定した場合</exception>
    /// <exception cref="InvalidOperationException">ホスト処理の結果がnull、または宣言された個数・型と一致しない場合</exception>
    /// <exception cref="WasmTrapException">Wasmの実行規則によるtrapが発生した場合</exception>
    /// <exception cref="WasmExhaustionException">呼び出し深さが上限に達したか、CLRスタックの余裕が不足した場合</exception>
    /// <exception cref="WasmImplementationLimitException">実行に必要なフレーム数または値の数が保持上限を超える場合</exception>
    public WasmResults Invoke(WasmInstance instance, ReadOnlySpan<WasmValue> arguments)
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

        if (this is InstanceHostFunction)
        {
            ArgumentNullException.ThrowIfNull(instance);
        }
        return ExecutionBoundary.Invoke(this, instance, arguments);
    }
}
