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
                var context = WasmExecutionContext.Enter(new(10), out var isOutermost);
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
                    context.Exit(isOutermost);
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
            new(ExecutionOpcode.Op41, WasmValue.FromExternRef(new object()), 81),
            new((ExecutionOpcode)int.MaxValue, default, 83),
        ]);
        var next = ExecutionFunctionFixture.Create([
            new(ExecutionOpcode.Op41, WasmValue.FromI32(42), 81),
            new(ExecutionOpcode.Op0B, default, 83),
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
                var context = WasmExecutionContext.Enter(new(10), out var isOutermost);
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
                    context.Exit(isOutermost);
                }
            })
            .ThrowsExactly<InvalidOperationException>();

        using (Assert.Multiple())
        {
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(clearedFrame.Function).IsNull();
            await Assert.That(clearedValue).IsEqualTo(default(WasmValue));
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
            new(ExecutionOpcode.Op41, WasmValue.FromI32(73), 81),
            new(ExecutionOpcode.Op0B, default, 83),
        ]);
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        var context = WasmExecutionContext.Enter(new(10), out var isOutermost);
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
            context.Exit(isOutermost);
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
        var context = WasmExecutionContext.Enter(new(1), out var isOutermost);
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
            context.Exit(isOutermost);
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
            [new(opcode, value, 12345678901), new(ExecutionOpcode.Op0B, default, 12345678909)],
            value.Kind
        );
        var context = WasmExecutionContext.Enter(new(10), out var isOutermost);
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
            context.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(1);
            await Assert.That(result.Values[0]).IsEqualTo(value);
            await Assert.That(restored).IsTrue();
            await Assert.That(context.GetValue(0)).IsEqualTo(default(WasmValue));
            await Assert.That(context.GetFrame(0).Function).IsNull();
        }
    }
}
