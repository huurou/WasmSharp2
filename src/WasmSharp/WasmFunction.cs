using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// functionを表現するクラス
/// </summary>
public sealed class WasmFunction
{
    /// <summary>
    /// この関数が所属するinstance
    /// </summary>
    internal WasmInstance Instance { get; }

    /// <summary>
    /// 所属するmoduleの関数index空間における位置
    /// </summary>
    internal uint FunctionIndex { get; }

    /// <summary>
    /// 所属するmoduleが保持するデコード済み関数定義
    /// </summary>
    internal DecodedFunction Definition => Instance.Module.Functions[(int)FunctionIndex];

    /// <summary>
    /// 所属するmoduleが同じ関数indexに保持する実行コード
    /// </summary>
    internal FunctionCode Code => Instance.Module.FunctionCodes[(int)FunctionIndex];

    /// <summary>
    /// functionの型
    /// </summary>
    public WasmFunctionType Type => Instance.Module.Types[(int)Definition.TypeIndex];

    /// <summary>
    /// instanceと関数indexを結び付けて関数の実体を構築する
    /// </summary>
    /// <param name="instance">この関数が所属するinstance</param>
    /// <param name="functionIndex">所属するmodule内の関数index</param>
    internal WasmFunction(WasmInstance instance, uint functionIndex)
    {
        Instance = instance;
        FunctionIndex = functionIndex;
    }

    /// <summary>
    /// 指定した引数で関数を呼び出す
    /// </summary>
    /// <param name="arguments">関数に渡すvalueのコレクション</param>
    /// <param name="options">実行時のオプション設定</param>
    /// <returns>関数が返すvalueのコレクション</returns>
    public WasmResults Invoke(
        ReadOnlySpan<WasmValue> arguments,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
