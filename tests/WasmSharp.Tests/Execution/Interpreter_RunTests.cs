using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_RunTests
{
    [Test]
    [Arguments(WasmProcessingStage.Invoke)]
    [Arguments(WasmProcessingStage.Instantiate)]
    public async Task 入口の必要容量が保持上限を超える_段階と関数位置を付けて伝播し外側を復元する(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create([], maxOperandStack: int.MaxValue);
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        ExecutionFrame actualFrame = default;
        WasmValue actualValue = default;
        (int Frames, int Values, int Depth) state = default;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var context = InterpreterContext.Enter(new(10), out var isOutermost);
                try
                {
                    context.EnsureCapacity(0, 1, new(stage));
                    context.TryEnterCall();
                    context.PushFrame(frame);
                    context.PushValue(value);
                    try
                    {
                        Interpreter.Run(context, function, [], stage);
                    }
                    finally
                    {
                        actualFrame = context.GetFrame(0);
                        actualValue = context.GetValue(0);
                        state = (context.FrameCount, context.ValueCount, context.CallDepth);
                    }
                }
                finally
                {
                    context.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                }
            })
            .ThrowsExactly<WasmImplementationLimitException>();

        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(stage, 12345678900, 1));
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(actualValue).IsEqualTo(value);
            await Assert.That(state).IsEqualTo((1, 1, 1));
        }
    }

    [Test]
    public async Task 生成ループ内で例外が発生する_途中のフレームと値を除き次の実行を妨げない()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create([
            new(ExecutionOpcode.Op41, WasmValue.FromExternRef(new object()), 81, 0),
            new((ExecutionOpcode)int.MaxValue, default, 83, 0),
        ]);
        var next = ExecutionFunctionFixture.Create([
            new(ExecutionOpcode.Op41, WasmValue.FromI32(42), 81, 0),
            new(ExecutionOpcode.Op0B, default, 83, 0),
        ]);
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        ExecutionFrame actualFrame = default;
        ExecutionFrame clearedFrame = default;
        WasmValue clearedValue = default;
        ExecutionResult nextResult = default;
        (int Frames, int Values, int Depth) state = default;

        // Act & Assert
        await Assert
            .That(() =>
            {
                var context = InterpreterContext.Enter(new(10), out var isOutermost);
                try
                {
                    context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
                    context.TryEnterCall();
                    context.PushFrame(frame);
                    context.PushValue(WasmValue.FromI32(73));
                    try
                    {
                        Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
                    }
                    finally
                    {
                        state = (context.FrameCount, context.ValueCount, context.CallDepth);
                        actualFrame = context.GetFrame(0);
                        clearedFrame = context.GetFrame(1);
                        clearedValue = context.GetValue(1);
                        nextResult = Interpreter.Run(context, next, [], WasmProcessingStage.Invoke);
                    }
                }
                finally
                {
                    context.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                }
            })
            .ThrowsExactly<InvalidOperationException>();

        using (Assert.Multiple())
        {
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(clearedFrame.Function).IsNull();
            await Assert.That(clearedValue).IsEqualTo(default);
            await Assert.That(state).IsEqualTo((1, 1, 1));
            await Assert.That(nextResult.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(nextResult.Values.Single().AsI32()).IsEqualTo(42);
        }
    }

    [Test]
    public async Task 外側のフレームを残して実行する_内側の終了で停止し外側の上限と状態を保つ()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create([
            new(ExecutionOpcode.Op41, WasmValue.FromI32(73), 81, 0),
            new(ExecutionOpcode.Op0B, default, 83, 0),
        ]);
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        ExecutionFrame actualFrame;
        WasmValue actualValue;
        (int Frames, int Values, int Depth, int Limit) state;

        // Act
        try
        {
            context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(value);
            result = Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
            actualFrame = context.GetFrame(0);
            actualValue = context.GetValue(0);
            state = (
                context.FrameCount,
                context.ValueCount,
                context.CallDepth,
                context.MaxCallDepth
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Single().AsI32()).IsEqualTo(73);
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(actualValue).IsEqualTo(value);
            await Assert.That(state).IsEqualTo((1, 1, 1, 10));
            await Assert.That(function.Instance.ExecutionOptions.MaxCallDepth).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 呼び出し深さが上限に達する_容量確保より先に原因と関数位置付きの失敗を返す()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create([], maxOperandStack: int.MaxValue);
        var context = InterpreterContext.Enter(new(1), out var isOutermost);
        ExecutionResult result;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            context.TryEnterCall();
            result = Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Exhaustion);
            await Assert
                .That(result.ExhaustionReason)
                .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(result.TrapReason).IsNull();
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(result.Limit).IsEqualTo(1);
            await Assert.That(result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(result.ByteOffset).IsEqualTo(12345678900);
            await Assert.That(state).IsEqualTo((0, 0, 1));
        }
    }

    [Test]
    [Arguments(ExecutionOpcode.Op41)]
    [Arguments(ExecutionOpcode.Op42)]
    [Arguments(ExecutionOpcode.Op43)]
    [Arguments(ExecutionOpcode.Op44)]
    public async Task 定数関数を実行する_生成ループの結果を復元後も保持する(ExecutionOpcode opcode)
    {
        // Arrange
        var value = opcode switch
        {
            ExecutionOpcode.Op41 => WasmValue.FromI32(int.MinValue),
            ExecutionOpcode.Op42 => WasmValue.FromI64(long.MinValue),
            ExecutionOpcode.Op43 => WasmValue.FromF32Bits(0xFFC12345),
            _ => WasmValue.FromF64Bits(0xFFF8123456789ABC),
        };
        var function = ExecutionFunctionFixture.Create(
            [
                new(opcode, value, 12345678901, 0),
                new(ExecutionOpcode.Op0B, default, 12345678909, 0),
            ],
            value.Kind
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        bool restored;

        // Act
        try
        {
            result = Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
            restored = context.FrameCount == 0 && context.ValueCount == 0 && context.CallDepth == 0;
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(1);
            await Assert.That(result.Values[0]).IsEqualTo(value);
            await Assert.That(restored).IsTrue();
            await Assert.That(context.GetValue(0)).IsEqualTo(default);
            await Assert.That(context.GetFrame(0).Function).IsNull();
        }
    }

    [Test]
    public async Task 引数と追加localsを持つ関数が複数の結果を残す_宣言結果だけを順序どおり返し外側を復元する()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [
                new(ExecutionOpcode.Op44, WasmValue.FromF64Bits(0x7FF4000000000001), 81, 0),
                new(ExecutionOpcode.Op42, WasmValue.FromI64(long.MinValue), 90, 0),
                new(ExecutionOpcode.Op41, WasmValue.FromI32(-1), 99, 0),
                new(ExecutionOpcode.Op0B, default, 101, 0),
            ],
            new(
                [WasmValueKind.I32, WasmValueKind.ExternRef],
                [WasmValueKind.F64, WasmValueKind.I64, WasmValueKind.I32]
            ),
            [new(2, WasmValueKind.V128)],
            3
        );
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        ExecutionFrame actualFrame;
        WasmValue actualValue;
        WasmValue cleared;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(value);
            result = Interpreter.Run(
                context,
                function,
                [WasmValue.FromI32(5), WasmValue.FromExternRef(new object())],
                WasmProcessingStage.Invoke
            );
            actualFrame = context.GetFrame(0);
            actualValue = context.GetValue(0);
            cleared = context.GetValue(1);
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(3);
            await Assert.That(result.Values[0].AsF64Bits()).IsEqualTo(0x7FF4000000000001UL);
            await Assert.That(result.Values[1].AsI64()).IsEqualTo(long.MinValue);
            await Assert.That(result.Values[2].AsI32()).IsEqualTo(-1);
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(actualValue).IsEqualTo(value);
            await Assert.That(cleared).IsEqualTo(default);
            await Assert.That(state).IsEqualTo((1, 1, 1));
        }
    }

    [Test]
    public async Task 引数があり結果が0個の関数を実行する_空の結果を返し引数を残さない()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [new(ExecutionOpcode.Op0B, default, 81, 0)],
            new([WasmValueKind.I64, WasmValueKind.FuncRef], []),
            [new(1, WasmValueKind.ExternRef)],
            0
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        WasmValue cleared;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                function,
                [WasmValue.FromI64(3), WasmValue.FromFuncRef(function)],
                WasmProcessingStage.Invoke
            );
            cleared = context.GetValue(1);
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.IsDefault).IsFalse();
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(cleared).IsEqualTo(default);
            await Assert.That(state).IsEqualTo((0, 0, 0));
        }
    }

    [Test]
    [Arguments(uint.MaxValue, 0u)]
    [Arguments(2_147_000_000u, 1_000_000u)]
    public async Task 追加localsを含む必要数が保持上限を超える_割当前に位置付き実装上限とし外側を復元する(
        uint firstCount,
        uint secondCount
    )
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [new(ExecutionOpcode.Op0B, default, 81, 0)],
            new([WasmValueKind.I32], []),
            [new(firstCount, WasmValueKind.I32), new(secondCount, WasmValueKind.I64)],
            1
        );
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        ExecutionFrame actualFrame = default;
        WasmValue actualValue = default;
        (int Frames, int Values, int Depth) state = default;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var context = InterpreterContext.Enter(new(10), out var isOutermost);
                try
                {
                    context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
                    context.TryEnterCall();
                    context.PushFrame(frame);
                    context.PushValue(value);
                    try
                    {
                        Interpreter.Run(
                            context,
                            function,
                            [WasmValue.FromI32(1)],
                            WasmProcessingStage.Invoke
                        );
                    }
                    finally
                    {
                        actualFrame = context.GetFrame(0);
                        actualValue = context.GetValue(0);
                        state = (context.FrameCount, context.ValueCount, context.CallDepth);
                    }
                }
                finally
                {
                    context.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                }
            })
            .ThrowsExactly<WasmImplementationLimitException>();

        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Invoke, 12345678900, 1));
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(actualValue).IsEqualTo(value);
            await Assert.That(state).IsEqualTo((1, 1, 1));
        }
    }
}
