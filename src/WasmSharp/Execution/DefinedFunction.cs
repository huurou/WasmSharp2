using WasmSharp.Modules;

namespace WasmSharp.Execution;

/// <summary>
/// 元のinstanceに所属し、Wasmの定義と実行コードを持つ関数
/// </summary>
/// <param name="instance">この定義関数が所属するinstance</param>
/// <param name="functionIndex">importを含むmodule全体の関数index</param>
/// <param name="definitionIndex">module内の定義配列の添字</param>
internal sealed class DefinedFunction(
    WasmInstance instance,
    uint functionIndex,
    uint definitionIndex
) : WasmFunction
{
    private readonly uint definitionIndex_ = definitionIndex;

    /// <summary>
    /// この関数が所属するinstance
    /// </summary>
    internal WasmInstance Instance { get; } = instance;

    /// <summary>
    /// 所属するmoduleの関数index空間における位置
    /// </summary>
    internal uint FunctionIndex { get; } = functionIndex;

    /// <summary>
    /// 所属するmoduleが保持するデコード済み関数定義
    /// </summary>
    internal DecodedFunction Definition => Instance.Module.Functions[(int)definitionIndex_];

    /// <summary>
    /// 所属するmoduleが定義配列の添字に保持する実行コード
    /// </summary>
    internal FunctionCode Code => Instance.Module.FunctionCodes[(int)definitionIndex_];

    /// <inheritdoc />
    public override WasmFunctionType Type => Instance.Module.Types[(int)Definition.TypeIndex];
}
