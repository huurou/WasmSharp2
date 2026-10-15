using System.Buffers.Binary;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceのメモリの現在のページ数をoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数を持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのmemory.size命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult Size(InterpreterContext context, in Instruction instruction)
    {
        var memory = context.CurrentFunction.Instance.Memories[0];
        context.PushValue(WasmValue.FromI32((int)memory.PageCount));
        return default;
    }

    /// <summary>
    /// 所属instanceのメモリを拡張し、拡張前のページ数か失敗値をoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数と追加ページ数を持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのmemory.grow命令</param>
    /// <returns>拡張の成否にかかわらず実行の成功</returns>
    internal static ExecutionResult Grow(InterpreterContext context, in Instruction instruction)
    {
        var memory = context.CurrentFunction.Instance.Memories[0];
        var deltaPages = unchecked((uint)context.PopValue().AsI32());
        bool success;
        uint previousPageCount;
        try
        {
            success = memory.TryGrow(deltaPages, out previousPageCount);
        }
        catch (OutOfMemoryException)
        {
            success = false;
            previousPageCount = 0;
        }

        context.PushValue(WasmValue.FromI32(success ? (int)previousPageCount : -1));
        return default;
    }

    /// <summary>
    /// 所属instanceのメモリから指定幅を読み、拡張した値をoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">即値とアクセス情報を保持する検証済みのロード命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult Load(InterpreterContext context, in Instruction instruction)
    {
        var signature = instruction.MemoryAccessSignature;
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        Span<byte> bytes = stackalloc byte[8];
        if (
            !context
                .CurrentFunction.Instance.Memories[0]
                .TryRead(address, bytes[..signature.ByteWidth])
        )
        {
            return ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }

        ulong bits = signature.ByteWidth switch
        {
            1 => bytes[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            _ => throw new InvalidOperationException("メモリアクセス幅が不正です。"),
        };
        if (signature.SignExtend)
        {
            // 読み込んだ幅の符号ビットを最上位へ移し、算術シフトで64ビットへ拡張する。
            var shift = 64 - signature.ByteWidth * 8;
            bits = unchecked((ulong)((long)(bits << shift) >> shift));
        }

        var value = signature.ValueType switch
        {
            WasmValueKind.I32 => WasmValue.FromI32(unchecked((int)bits)),
            WasmValueKind.I64 => WasmValue.FromI64(unchecked((long)bits)),
            WasmValueKind.F32 => WasmValue.FromF32Bits((uint)bits),
            WasmValueKind.F64 => WasmValue.FromF64Bits(bits),
            _ => throw new InvalidOperationException("メモリアクセスの値型が不正です。"),
        };
        context.PushValue(value);
        return default;
    }

    /// <summary>
    /// operandの値の下位ビットを所属instanceのメモリへ指定幅で書き込む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレス・値を持つ実行コンテキスト</param>
    /// <param name="instruction">即値とアクセス情報を保持する検証済みのストア命令</param>
    /// <returns>実行の成功またはメモリを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult Store(InterpreterContext context, in Instruction instruction)
    {
        var signature = instruction.MemoryAccessSignature;
        var value = context.PopValue();
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        var bits = signature.ValueType switch
        {
            WasmValueKind.I32 => unchecked((uint)value.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
            WasmValueKind.F32 => value.AsF32Bits(),
            WasmValueKind.F64 => value.AsF64Bits(),
            _ => throw new InvalidOperationException("メモリアクセスの値型が不正です。"),
        };
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, bits);
        if (
            !context
                .CurrentFunction.Instance.Memories[0]
                .TryWrite(address, bytes[..signature.ByteWidth])
        )
        {
            return ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }

        return default;
    }
}
