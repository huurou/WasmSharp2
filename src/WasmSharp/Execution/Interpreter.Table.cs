using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceの指定table間で参照列をコピーする
    /// </summary>
    /// <param name="context">現在の関数とコピー先・コピー元・要素数を持つ実行コンテキスト</param>
    /// <param name="instruction">コピー先とコピー元のtable添字を持つ検証済みのtable.copy命令</param>
    /// <returns>実行の成功またはtableを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult TableCopy(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var function = context.CurrentFunction;
        var length = unchecked((uint)context.PopValue().AsI32());
        var sourceOffset = unchecked((uint)context.PopValue().AsI32());
        var destination = unchecked((uint)context.PopValue().AsI32());
        var tables = function.Instance.Tables;
        if (
            !tables[(int)instruction.Index]
                .TryCopy(tables[(int)instruction.SecondaryIndex], destination, sourceOffset, length)
        )
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
    /// 所属instanceの指定tableの範囲を同じ参照値で埋める
    /// </summary>
    /// <param name="context">現在の関数とコピー先・参照・要素数を持つ実行コンテキスト</param>
    /// <param name="instruction">table添字を持つ検証済みのtable.fill命令</param>
    /// <returns>実行の成功またはtableを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult TableFill(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var function = context.CurrentFunction;
        var length = unchecked((uint)context.PopValue().AsI32());
        var value = context.PopValue();
        var destination = unchecked((uint)context.PopValue().AsI32());
        if (!function.Instance.Tables[(int)instruction.Index].TryFill(destination, value, length))
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
    /// 所属instanceの要素セグメントから指定tableへ参照列を転送する
    /// </summary>
    /// <param name="context">現在の関数とコピー先・コピー元・要素数を持つ実行コンテキスト</param>
    /// <param name="instruction">要素とtableの添字を持つ検証済みのtable.init命令</param>
    /// <returns>実行の成功またはtableを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult TableInit(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var function = context.CurrentFunction;
        var length = unchecked((uint)context.PopValue().AsI32());
        var source = unchecked((uint)context.PopValue().AsI32());
        var destination = unchecked((uint)context.PopValue().AsI32());
        var instance = function.Instance;
        if (
            !instance
                .ElementSegments[(int)instruction.Index]
                .TryInitialize(
                    instance.Tables[(int)instruction.SecondaryIndex],
                    destination,
                    source,
                    length
                )
        )
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
    /// 所属instanceの指定要素セグメントを破棄する
    /// </summary>
    /// <param name="context">現在の関数を持つ実行コンテキスト</param>
    /// <param name="instruction">要素添字を持つ検証済みのelem.drop命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult ElementDrop(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        context.CurrentFunction.Instance.ElementSegments[(int)instruction.Index].Drop();
        return default;
    }

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
