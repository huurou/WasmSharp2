namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// i32を8bitへ切り詰め全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 8, unchecked((uint)context.PopValue().AsI32()));
    }

    /// <summary>
    /// i32を16bitへ切り詰め全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 16, unchecked((uint)context.PopValue().AsI32()));
    }

    /// <summary>
    /// i32のビット列を全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 32, unchecked((uint)context.PopValue().AsI32()));
    }

    /// <summary>
    /// i64のビット列を全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 64, unchecked((ulong)context.PopValue().AsI64()));
    }

    /// <summary>
    /// f32のビット列を変えず全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 32, context.PopValue().AsF32Bits());
    }

    /// <summary>
    /// f64のビット列を変えず全laneへ複製する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Splat命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Splat(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return SplatSimdLane(context, 64, context.PopValue().AsF64Bits());
    }

    /// <summary>
    /// 8bitのlaneを符号拡張しi32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16ExtractLaneS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16ExtractLaneS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 8);
        context.PushValue(WasmValue.FromI32(unchecked((sbyte)bits)));
        return default;
    }

    /// <summary>
    /// 8bitのlaneをゼロ拡張しi32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16ExtractLaneU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16ExtractLaneU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 8);
        context.PushValue(WasmValue.FromI32((byte)bits));
        return default;
    }

    /// <summary>
    /// 16bitのlaneを符号拡張しi32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtractLaneS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtractLaneS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 16);
        context.PushValue(WasmValue.FromI32(unchecked((short)bits)));
        return default;
    }

    /// <summary>
    /// 16bitのlaneをゼロ拡張しi32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtractLaneU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtractLaneU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 16);
        context.PushValue(WasmValue.FromI32((ushort)bits));
        return default;
    }

    /// <summary>
    /// 32bitのlaneをi32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtractLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtractLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 32);
        context.PushValue(WasmValue.FromI32(unchecked((int)bits)));
        return default;
    }

    /// <summary>
    /// 64bitのlaneをi64として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtractLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtractLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 64);
        context.PushValue(WasmValue.FromI64(unchecked((long)bits)));
        return default;
    }

    /// <summary>
    /// 32bitのlaneをビット列を変えずf32として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4ExtractLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4ExtractLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 32);
        context.PushValue(WasmValue.FromF32Bits((uint)bits));
        return default;
    }

    /// <summary>
    /// 64bitのlaneをビット列を変えずf64として取り出す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2ExtractLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2ExtractLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = ExtractSimdLane(context, in instruction, 64);
        context.PushValue(WasmValue.FromF64Bits(bits));
        return default;
    }

    /// <summary>
    /// i32を8bitへ切り詰め指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = unchecked((uint)context.PopValue().AsI32());
        return ReplaceSimdLane(context, in instruction, 8, bits);
    }

    /// <summary>
    /// i32を16bitへ切り詰め指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = unchecked((uint)context.PopValue().AsI32());
        return ReplaceSimdLane(context, in instruction, 16, bits);
    }

    /// <summary>
    /// i32のビット列で指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = unchecked((uint)context.PopValue().AsI32());
        return ReplaceSimdLane(context, in instruction, 32, bits);
    }

    /// <summary>
    /// i64のビット列で指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = unchecked((ulong)context.PopValue().AsI64());
        return ReplaceSimdLane(context, in instruction, 64, bits);
    }

    /// <summary>
    /// f32のビット列を変えず指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = context.PopValue().AsF32Bits();
        return ReplaceSimdLane(context, in instruction, 32, bits);
    }

    /// <summary>
    /// f64のビット列を変えず指定laneだけを置換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2ReplaceLane命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2ReplaceLane(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var bits = context.PopValue().AsF64Bits();
        return ReplaceSimdLane(context, in instruction, 64, bits);
    }

    /// <summary>
    /// 2個のvectorを連結したbyte列から即値で選択する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Shuffle命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Shuffle(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var second = SimdValue.FromValue(context.PopValue());
        var first = SimdValue.FromValue(context.PopValue());
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 16; laneIndex++)
        {
            var index = instruction.ShuffleLanes[laneIndex];
            var bits = index < 16 ? first.GetLane(8, index) : second.GetLane(8, index - 16);
            result = result.WithLane(8, laneIndex, bits);
        }

        context.PushValue(result.ToValue());
        return default;
    }

    /// <summary>
    /// index vectorに従ってbyteを選択し範囲外をゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Swizzle命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Swizzle(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var indices = SimdValue.FromValue(context.PopValue());
        var vector = SimdValue.FromValue(context.PopValue());
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 16; laneIndex++)
        {
            var index = indices.GetLane(8, laneIndex);
            var bits = index < 16 ? vector.GetLane(8, (int)index) : 0;
            result = result.WithLane(8, laneIndex, bits);
        }

        context.PushValue(result.ToValue());
        return default;
    }

    /// <summary>
    /// 128bitすべてのbitを反転する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128Not命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128Not(InterpreterContext context, in Instruction instruction)
    {
        var vector = context.PopValue().AsV128();
        context.PushValue(WasmValue.FromV128(~vector.Low64, ~vector.High64));
        return default;
    }

    /// <summary>
    /// 2個のvectorのbitごとの論理積を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128And命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128And(InterpreterContext context, in Instruction instruction)
    {
        var second = context.PopValue().AsV128();
        var first = context.PopValue().AsV128();
        context.PushValue(
            WasmValue.FromV128(first.Low64 & second.Low64, first.High64 & second.High64)
        );
        return default;
    }

    /// <summary>
    /// 第1vectorと第2vectorを反転したbit列の論理積を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128AndNot命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128AndNot(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var second = context.PopValue().AsV128();
        var first = context.PopValue().AsV128();
        context.PushValue(
            WasmValue.FromV128(first.Low64 & ~second.Low64, first.High64 & ~second.High64)
        );
        return default;
    }

    /// <summary>
    /// 2個のvectorのbitごとの論理和を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128Or命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128Or(InterpreterContext context, in Instruction instruction)
    {
        var second = context.PopValue().AsV128();
        var first = context.PopValue().AsV128();
        context.PushValue(
            WasmValue.FromV128(first.Low64 | second.Low64, first.High64 | second.High64)
        );
        return default;
    }

    /// <summary>
    /// 2個のvectorのbitごとの排他的論理和を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128Xor命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128Xor(InterpreterContext context, in Instruction instruction)
    {
        var second = context.PopValue().AsV128();
        var first = context.PopValue().AsV128();
        context.PushValue(
            WasmValue.FromV128(first.Low64 ^ second.Low64, first.High64 ^ second.High64)
        );
        return default;
    }

    /// <summary>
    /// maskが1のbitを第1vectorから選択する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128Bitselect命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128Bitselect(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var mask = context.PopValue().AsV128();
        var second = context.PopValue().AsV128();
        var first = context.PopValue().AsV128();
        context.PushValue(
            WasmValue.FromV128(
                (first.Low64 & mask.Low64) | (second.Low64 & ~mask.Low64),
                (first.High64 & mask.High64) | (second.High64 & ~mask.High64)
            )
        );
        return default;
    }

    /// <summary>
    /// 8bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Eq(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x == y);
    }

    /// <summary>
    /// 8bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Ne(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x != y);
    }

    /// <summary>
    /// 8bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16LtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) < unchecked((sbyte)y)
        );
    }

    /// <summary>
    /// 8bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16LtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16LtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x < y);
    }

    /// <summary>
    /// 8bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16GtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) > unchecked((sbyte)y)
        );
    }

    /// <summary>
    /// 8bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16GtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16GtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x > y);
    }

    /// <summary>
    /// 8bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16LeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) <= unchecked((sbyte)y)
        );
    }

    /// <summary>
    /// 8bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16LeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16LeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x <= y);
    }

    /// <summary>
    /// 8bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16GeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) >= unchecked((sbyte)y)
        );
    }

    /// <summary>
    /// 8bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16GeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16GeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 8, static (x, y) => x >= y);
    }

    /// <summary>
    /// 16bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Eq(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x == y);
    }

    /// <summary>
    /// 16bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Ne(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x != y);
    }

    /// <summary>
    /// 16bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8LtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            16,
            static (x, y) => unchecked((short)x) < unchecked((short)y)
        );
    }

    /// <summary>
    /// 16bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8LtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8LtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x < y);
    }

    /// <summary>
    /// 16bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8GtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            16,
            static (x, y) => unchecked((short)x) > unchecked((short)y)
        );
    }

    /// <summary>
    /// 16bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8GtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8GtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x > y);
    }

    /// <summary>
    /// 16bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8LeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            16,
            static (x, y) => unchecked((short)x) <= unchecked((short)y)
        );
    }

    /// <summary>
    /// 16bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8LeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8LeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x <= y);
    }

    /// <summary>
    /// 16bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8GeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            16,
            static (x, y) => unchecked((short)x) >= unchecked((short)y)
        );
    }

    /// <summary>
    /// 16bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8GeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8GeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 16, static (x, y) => x >= y);
    }

    /// <summary>
    /// 32bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Eq(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x == y);
    }

    /// <summary>
    /// 32bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Ne(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x != y);
    }

    /// <summary>
    /// 32bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4LtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            32,
            static (x, y) => unchecked((int)x) < unchecked((int)y)
        );
    }

    /// <summary>
    /// 32bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4LtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4LtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x < y);
    }

    /// <summary>
    /// 32bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4GtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            32,
            static (x, y) => unchecked((int)x) > unchecked((int)y)
        );
    }

    /// <summary>
    /// 32bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4GtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4GtU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x > y);
    }

    /// <summary>
    /// 32bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4LeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            32,
            static (x, y) => unchecked((int)x) <= unchecked((int)y)
        );
    }

    /// <summary>
    /// 32bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4LeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4LeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x <= y);
    }

    /// <summary>
    /// 32bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4GeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            32,
            static (x, y) => unchecked((int)x) >= unchecked((int)y)
        );
    }

    /// <summary>
    /// 32bitのlaneを符号なし比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4GeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4GeU(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 32, static (x, y) => x >= y);
    }

    /// <summary>
    /// 64bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Eq(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 64, static (x, y) => x == y);
    }

    /// <summary>
    /// 64bitのlaneをビット列で比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Ne(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(context, 64, static (x, y) => x != y);
    }

    /// <summary>
    /// 64bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2LtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            64,
            static (x, y) => unchecked((long)x) < unchecked((long)y)
        );
    }

    /// <summary>
    /// 64bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2GtS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            64,
            static (x, y) => unchecked((long)x) > unchecked((long)y)
        );
    }

    /// <summary>
    /// 64bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2LeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            64,
            static (x, y) => unchecked((long)x) <= unchecked((long)y)
        );
    }

    /// <summary>
    /// 64bitのlaneを符号付き比較し全bit1または0を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2GeS(InterpreterContext context, in Instruction instruction)
    {
        return CompareSimdLanes(
            context,
            64,
            static (x, y) => unchecked((long)x) >= unchecked((long)y)
        );
    }

    /// <summary>
    /// 128bitのいずれかのbitが非ゼロならi32の1を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのV128AnyTrue命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult V128AnyTrue(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return AnyTrueSimdLanes(context);
    }

    /// <summary>
    /// すべてのlaneが非ゼロならi32の1を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16AllTrue命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16AllTrue(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return AllTrueSimdLanes(context, 8);
    }

    /// <summary>
    /// 各laneの最上位bitをlane番号順にi32へ集める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Bitmask命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Bitmask(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return BitmaskSimdLanes(context, 8);
    }

    /// <summary>
    /// すべてのlaneが非ゼロならi32の1を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8AllTrue命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8AllTrue(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return AllTrueSimdLanes(context, 16);
    }

    /// <summary>
    /// 各laneの最上位bitをlane番号順にi32へ集める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Bitmask命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Bitmask(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return BitmaskSimdLanes(context, 16);
    }

    /// <summary>
    /// すべてのlaneが非ゼロならi32の1を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4AllTrue命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4AllTrue(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return AllTrueSimdLanes(context, 32);
    }

    /// <summary>
    /// 各laneの最上位bitをlane番号順にi32へ集める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Bitmask命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Bitmask(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return BitmaskSimdLanes(context, 32);
    }

    /// <summary>
    /// すべてのlaneが非ゼロならi32の1を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2AllTrue命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2AllTrue(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return AllTrueSimdLanes(context, 64);
    }

    /// <summary>
    /// 各laneの最上位bitをlane番号順にi32へ集める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Bitmask命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Bitmask(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return BitmaskSimdLanes(context, 64);
    }

    /// <summary>
    /// 同じ幅のlaneに比較を適用し真偽を全bit1または0で保持する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="compare">handlerで符号を解釈する比較</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult CompareSimdLanes(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, ulong, bool> compare
    )
    {
        var second = SimdValue.FromValue(context.PopValue());
        var first = SimdValue.FromValue(context.PopValue());
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            var matched = compare(
                first.GetLane(bitWidth, laneIndex),
                second.GetLane(bitWidth, laneIndex)
            );
            result = result.WithLane(bitWidth, laneIndex, matched ? ulong.MaxValue : 0);
        }

        context.PushValue(result.ToValue());
        return default;
    }

    /// <summary>
    /// 128bit全体の非ゼロ判定をi32で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult AnyTrueSimdLanes(InterpreterContext context)
    {
        var vector = context.PopValue().AsV128();
        context.PushValue(WasmValue.FromI32((vector.Low64 | vector.High64) != 0 ? 1 : 0));
        return default;
    }

    /// <summary>
    /// すべてのlaneの非ゼロ判定をi32で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult AllTrueSimdLanes(InterpreterContext context, int bitWidth)
    {
        var vector = SimdValue.FromValue(context.PopValue());
        var allTrue = true;
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            allTrue &= vector.GetLane(bitWidth, laneIndex) != 0;
        }

        context.PushValue(WasmValue.FromI32(allTrue ? 1 : 0));
        return default;
    }

    /// <summary>
    /// 各laneの最上位bitをi32の対応位置へ集める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult BitmaskSimdLanes(InterpreterContext context, int bitWidth)
    {
        var vector = SimdValue.FromValue(context.PopValue());
        var mask = 0;
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            var signBit = vector.GetLane(bitWidth, laneIndex) >> (bitWidth - 1);
            mask |= (int)signBit << laneIndex;
        }

        context.PushValue(WasmValue.FromI32(mask));
        return default;
    }

    /// <summary>
    /// 指定幅へ切り詰めたビット列を128bit全体へ複製する
    /// </summary>
    /// <param name="context">結果を積む実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="bits">複製するスカラーのビット列</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult SplatSimdLane(
        InterpreterContext context,
        int bitWidth,
        ulong bits
    )
    {
        var vector = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            vector = vector.WithLane(bitWidth, laneIndex, bits);
        }

        context.PushValue(vector.ToValue());
        return default;
    }

    /// <summary>
    /// operandの指定laneから符号解釈前のビット列を取り出す
    /// </summary>
    /// <param name="context">vectorのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みlane即値を保持する命令</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <returns>指定laneのビット列</returns>
    private static ulong ExtractSimdLane(
        InterpreterContext context,
        in Instruction instruction,
        int bitWidth
    )
    {
        return SimdValue.FromValue(context.PopValue()).GetLane(bitWidth, instruction.LaneIndex);
    }

    /// <summary>
    /// 指定laneだけを置換し他のビット列を保持する
    /// </summary>
    /// <param name="context">vectorのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みlane即値を保持する命令</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="bits">置換するスカラーのビット列</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ReplaceSimdLane(
        InterpreterContext context,
        in Instruction instruction,
        int bitWidth,
        ulong bits
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(vector.WithLane(bitWidth, instruction.LaneIndex, bits).ToValue());
        return default;
    }
}
