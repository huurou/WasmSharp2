using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceのメモリから16バイトを読み、v128の値をoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのv128.load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load(InterpreterContext context, in Instruction instruction)
    {
        Span<byte> bytes = stackalloc byte[16];
        var result = ReadSimdMemory(context, in instruction, bytes);
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        context.PushValue(SimdValue.FromLittleEndian(bytes).ToValue());
        return default;
    }

    /// <summary>
    /// operandのv128の値を所属instanceのメモリへ16バイトで書き込む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレス・値を持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのv128.store命令</param>
    /// <returns>実行の成功またはメモリを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Store(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var value = SimdValue.FromValue(context.PopValue());
        Span<byte> bytes = stackalloc byte[16];
        value.WriteLittleEndian(bytes);
        return WriteSimdMemory(context, in instruction, bytes);
    }

    /// <summary>
    /// 指定幅だけをメモリから読み、operandのv128の対象laneを置き換える
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレス・vectorを持つ実行コンテキスト</param>
    /// <param name="instruction">アクセス幅とlane indexを持つ検証済みのload_lane命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128LoadLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var value = SimdValue.FromValue(context.PopValue());
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        var byteWidth = instruction.MemoryAccessSignature.ByteWidth;
        var result = ReadSimdMemory(context, in instruction, bytes[..byteWidth]);
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        var bitWidth = byteWidth * 8;
        var bits = SimdValue.FromLittleEndian(bytes).GetLane(bitWidth, 0);
        context.PushValue(value.WithLane(bitWidth, instruction.LaneIndex, bits).ToValue());
        return default;
    }

    /// <summary>
    /// operandのv128の対象laneを指定幅でメモリへ書き込む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレス・vectorを持つ実行コンテキスト</param>
    /// <param name="instruction">アクセス幅とlane indexを持つ検証済みのstore_lane命令</param>
    /// <returns>実行の成功またはメモリを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128StoreLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var value = SimdValue.FromValue(context.PopValue());
        Span<byte> bytes = stackalloc byte[16];
        value.WriteLittleEndian(bytes);
        var byteWidth = instruction.MemoryAccessSignature.ByteWidth;
        return WriteSimdMemory(
            context,
            in instruction,
            bytes.Slice(instruction.LaneIndex * byteWidth, byteWidth)
        );
    }

    /// <summary>
    /// 8byteの各8bit整数を符号拡張し、8個の16bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load8x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 8, true);
    }

    /// <summary>
    /// 8byteの各8bit整数をゼロ拡張し、8個の16bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load8x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 8, false);
    }

    /// <summary>
    /// 8byteの各16bit整数を符号拡張し、4個の32bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load16x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 16, true);
    }

    /// <summary>
    /// 8byteの各16bit整数をゼロ拡張し、4個の32bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load16x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 16, false);
    }

    /// <summary>
    /// 8byteの各32bit整数を符号拡張し、2個の64bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load32x2S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 32, true);
    }

    /// <summary>
    /// 8byteの各32bit整数をゼロ拡張し、2個の64bit laneとして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みの拡張load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load32x2U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return LoadSimdMemoryExtended(context, in instruction, 32, false);
    }

    /// <summary>
    /// 指定幅の整数をメモリから読み、同じビット列を全laneへ複製する
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">1・2・4・8byteを指定する検証済みのsplat load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128LoadSplat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        var byteWidth = instruction.MemoryAccessSignature.ByteWidth;
        var result = ReadSimdMemory(context, in instruction, bytes[..byteWidth]);
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        var bitWidth = byteWidth * 8;
        var bits = SimdValue.FromLittleEndian(bytes).GetLane(bitWidth, 0);
        context.PushValue(SplatSimdMemoryLane(bits, bitWidth).ToValue());
        return default;
    }

    /// <summary>
    /// 指定幅のビット列を全laneへ複製する
    /// </summary>
    private static SimdValue SplatSimdMemoryLane(ulong bits, int bitWidth)
    {
        var value = default(SimdValue);
        for (var lane = 0; lane < 128 / bitWidth; lane++)
        {
            value = value.WithLane(bitWidth, lane, bits);
        }

        return value;
    }

    /// <summary>
    /// 4または8byteをメモリから読み、残りの上位bitをゼロにして積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">lane即値を持たない検証済みのzero load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128LoadZero(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        var result = ReadSimdMemory(
            context,
            in instruction,
            bytes[..instruction.MemoryAccessSignature.ByteWidth]
        );
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        context.PushValue(SimdValue.FromLittleEndian(bytes).ToValue());
        return default;
    }

    /// <summary>
    /// 8byteに並ぶ整数を2倍のlane幅へ拡張する
    /// </summary>
    private static ExecutionResult LoadSimdMemoryExtended(
        InterpreterContext context,
        in Instruction instruction,
        int bitWidth,
        bool signed
    )
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        var result = ReadSimdMemory(
            context,
            in instruction,
            bytes[..instruction.MemoryAccessSignature.ByteWidth]
        );
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        context.PushValue(
            ExtendSimdMemoryLanes(SimdValue.FromLittleEndian(bytes), bitWidth, signed).ToValue()
        );
        return default;
    }

    /// <summary>
    /// 下位64bitの整数laneを符号指定に従って2倍の幅へ拡張する
    /// </summary>
    private static SimdValue ExtendSimdMemoryLanes(SimdValue source, int bitWidth, bool signed)
    {
        var value = default(SimdValue);
        for (var lane = 0; lane < 64 / bitWidth; lane++)
        {
            var bits = source.GetLane(bitWidth, lane);
            if (signed)
            {
                // 入力laneの符号bitを最上位へ移し、算術右シフトで符号拡張する
                var shift = 64 - bitWidth;
                bits = unchecked((ulong)((long)(bits << shift) >> shift));
            }

            value = value.WithLane(bitWidth * 2, lane, bits);
        }

        return value;
    }

    /// <summary>
    /// operandの符号なしアドレスとoffsetから指定範囲を読み、範囲外の位置情報を保持する
    /// </summary>
    private static ExecutionResult ReadSimdMemory(
        InterpreterContext context,
        in Instruction instruction,
        Span<byte> destination
    )
    {
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        return context.CurrentFunction.Instance.Memories[0].TryRead(address, destination)
            ? default
            : ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
    }

    /// <summary>
    /// operandの符号なしアドレスとoffsetへ指定範囲を書き、範囲外の位置情報を保持する
    /// </summary>
    private static ExecutionResult WriteSimdMemory(
        InterpreterContext context,
        in Instruction instruction,
        ReadOnlySpan<byte> source
    )
    {
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        return context.CurrentFunction.Instance.Memories[0].TryWrite(address, source)
            ? default
            : ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
    }
}
