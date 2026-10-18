using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceの指定tableから参照を取得してoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数と要素添字を持つ実行コンテキスト</param>
    /// <param name="instruction">table添字を持つ検証済みのtable.get命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult TableGet(InterpreterContext context, in Instruction instruction)
    {
        var function = context.CurrentFunction;
        var index = unchecked((uint)context.PopValue().AsI32());
        if (!function.Instance.Tables[(int)instruction.Index].TryGet(index, out var value))
        {
            return ExecutionResult.Trap(
                WasmTrapReason.TableOutOfBounds,
                function.FunctionIndex,
                instruction.ByteOffset
            );
        }

        context.PushValue(value);
        return default;
    }

    /// <summary>
    /// 所属instanceの指定tableへ参照を設定する
    /// </summary>
    /// <param name="context">現在の関数と要素添字・参照を持つ実行コンテキスト</param>
    /// <param name="instruction">table添字を持つ検証済みのtable.set命令</param>
    /// <returns>実行の成功またはtableを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult TableSet(InterpreterContext context, in Instruction instruction)
    {
        var function = context.CurrentFunction;
        var value = context.PopValue();
        var index = unchecked((uint)context.PopValue().AsI32());
        if (!function.Instance.Tables[(int)instruction.Index].TrySet(index, value))
        {
            return ExecutionResult.Trap(
                WasmTrapReason.TableOutOfBounds,
                function.FunctionIndex,
                instruction.ByteOffset
            );
        }

        return default;
    }

    /// <summary>
    /// 所属instanceの指定tableの現在の要素数をi32のビット列で積む
    /// </summary>
    /// <param name="context">現在の関数を持つ実行コンテキスト</param>
    /// <param name="instruction">table添字を持つ検証済みのtable.size命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult TableSize(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var table = context.CurrentFunction.Instance.Tables[(int)instruction.Index];
        context.PushValue(WasmValue.FromI32(unchecked((int)table.Count)));
        return default;
    }

    /// <summary>
    /// 所属instanceの指定tableを増大し、旧要素数または失敗値を積む
    /// </summary>
    /// <param name="context">現在の関数と初期参照・増分を持つ実行コンテキスト</param>
    /// <param name="instruction">table添字を持つ検証済みのtable.grow命令</param>
    /// <returns>増大の成否にかかわらず実行の成功</returns>
    internal static ExecutionResult TableGrow(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var table = context.CurrentFunction.Instance.Tables[(int)instruction.Index];
        var delta = unchecked((uint)context.PopValue().AsI32());
        var value = context.PopValue();
        bool success;
        uint previousCount;
        try
        {
            success = table.TryGrow(delta, value, out previousCount);
        }
        catch (OutOfMemoryException)
        {
            success = false;
            previousCount = 0;
        }

        context.PushValue(WasmValue.FromI32(success ? unchecked((int)previousCount) : -1));
        return default;
    }
}
