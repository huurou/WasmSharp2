using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class InterpreterContext_CalculateCapacityTests
{
    [Test]
    [Arguments(0, 0UL, 0)]
    [Arguments(0, 3UL, 3)]
    [Arguments(8, 4UL, 8)]
    [Arguments(8, 9UL, 16)]
    [Arguments(8, 20UL, 20)]
    public async Task 必要数と現在容量を指定する_不足時だけ必要数か倍増後の大きい方を返す(
        int capacity,
        ulong requiredCount,
        int expected
    )
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);

        // Act
        var actual = InterpreterContext.CalculateCapacity(capacity, requiredCount, location);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task 倍増だけが保持上限を超える_必要数が収まれば上限までに抑える()
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);

        // Act
        var capacity = InterpreterContext.CalculateCapacity(
            Array.MaxLength - 1,
            (ulong)Array.MaxLength,
            location
        );

        // Assert
        await Assert.That(capacity).IsEqualTo(Array.MaxLength);
    }

    [Test]
    [Arguments(uint.MaxValue)]
    [Arguments(ulong.MaxValue)]
    public async Task 必要数が保持上限を超える_巡回させず実装上限として拒否する(ulong requiredCount)
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);

        // Act & Assert
        var exception = await Assert
            .That(() => InterpreterContext.CalculateCapacity(8, requiredCount, location))
            .ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert.That(exception.Location).IsEqualTo(location);
        }
    }
}

internal class InterpreterContext_EnsureCapacityTests
{
    [Test]
    public async Task 必要数の加算がintの上限を超える_位置付き保持上限を伝播して外側の状態を保つ()
    {
        // Arrange
        var function = FunctionFixture.Create();
        var location = new WasmFailureLocation(WasmProcessingStage.Instantiate, 30, 0);
        WasmValue value = default;
        var depth = -1;
        var frameCount = -1;
        var restored = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var context = InterpreterContext.Enter(new(10), out var isOutermost);
                try
                {
                    context.EnsureCapacity(0, 1, location);
                    context.TryEnterCall();
                    context.PushFrame(new(function, 0, 0));
                    context.PushValue(WasmValue.FromI32(42));
                    try
                    {
                        context.EnsureCapacity(int.MaxValue, int.MaxValue, location);
                    }
                    finally
                    {
                        value = context.GetValue(0);
                        depth = context.CallDepth;
                        frameCount = context.FrameCount;
                    }
                }
                finally
                {
                    context.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                    restored =
                        context.FrameCount == 0
                        && context.ValueCount == 0
                        && context.CallDepth == 0
                        && InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmImplementationLimitException>();

        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(value.AsI32()).IsEqualTo(42);
            await Assert.That(depth).IsEqualTo(1);
            await Assert.That(frameCount).IsEqualTo(1);
            await Assert.That(restored).IsTrue();
        }
    }
}

internal class InterpreterContext_RestoreTests
{
    [Test]
    public async Task 内側で領域を拡張して復元する_外側の値とフレームを保ち除いた参照を解除する()
    {
        // Arrange
        var module = new WasmModule(
            [
                new([WasmValueKind.I64], [WasmValueKind.ExternRef]),
                new([WasmValueKind.ExternRef], [WasmValueKind.I32]),
            ],
            [
                new(0, 30, [new(1, WasmValueKind.F32)], []),
                new(1, 80, [new(1, WasmValueKind.I32)], []),
            ],
            [],
            90,
            [],
            [],
            [],
            [],
            null
        );
        var instance = new WasmInstance(module, WasmExecutionOptions.Default);
        var function = (DefinedFunction)instance.Functions[0];
        var innerFunction = (DefinedFunction)instance.Functions[1];
        var reference = new object();
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);
        ExecutionFrame outerFrame;
        ExecutionFrame innerFrame;
        ExecutionFrame removedFrame;
        WasmValue argument;
        WasmValue local;
        WasmValue operand;
        WasmValue removedValue;
        WasmValue innerValue;
        int frameCount;
        int valueCount;
        int depth;
        bool fullyRestored;

        // Act
        try
        {
            context.EnsureCapacity(2, 1, location);
            context.TryEnterCall();
            context.PushFrame(new(function, 0, 2) { Pc = 1 });
            context.PushValue(WasmValue.FromI64(17));
            context.PushValue(WasmValue.FromF32Bits(0x80000000));
            context.PushValue(WasmValue.FromExternRef(reference));

            // 引数・localsの後ろにoperand領域を確保し、両方の配列を拡張する。
            context.EnsureCapacity(5, 8, location);
            context.TryEnterCall();
            context.PushFrame(new(innerFunction, 3, 5) { Pc = 7 });
            context.PushValue(WasmValue.FromExternRef(new object()));
            context.PushValue(WasmValue.FromI32(19));
            for (var i = 0; i < 8; i++)
            {
                context.PushValue(WasmValue.FromI32(i));
            }
            innerFrame = context.GetFrame(1);
            innerValue = context.GetValue(12);

            context.Restore(1, 3, 1);
            outerFrame = context.GetFrame(0);
            argument = context.GetValue(0);
            local = context.GetValue(1);
            operand = context.GetValue(2);
            removedFrame = context.GetFrame(1);
            removedValue = context.GetValue(3);
            frameCount = context.FrameCount;
            valueCount = context.ValueCount;
            depth = context.CallDepth;
        }
        finally
        {
            context.Restore(0, 0, 0);
            fullyRestored =
                context.FrameCount == 0 && context.ValueCount == 0 && context.CallDepth == 0;
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(outerFrame.Function, function)).IsTrue();
            await Assert.That(outerFrame.Pc).IsEqualTo(1);
            await Assert.That(outerFrame.Function.Definition.BodyOffset).IsEqualTo(30L);
            await Assert.That(outerFrame.StackBase).IsEqualTo(0);
            await Assert.That(outerFrame.OperandBase).IsEqualTo(2);
            await Assert.That(ReferenceEquals(innerFrame.Function, innerFunction)).IsTrue();
            await Assert.That(innerFrame.Pc).IsEqualTo(7);
            await Assert.That(innerFrame.Function.Definition.BodyOffset).IsEqualTo(80L);
            await Assert.That(innerFrame.StackBase).IsEqualTo(3);
            await Assert.That(innerFrame.OperandBase).IsEqualTo(5);
            await Assert.That(innerValue.AsI32()).IsEqualTo(7);
            await Assert.That(argument.AsI64()).IsEqualTo(17L);
            await Assert.That(local.AsF32Bits()).IsEqualTo(0x80000000U);
            await Assert.That(ReferenceEquals(operand.AsExternRef(), reference)).IsTrue();
            await Assert.That(frameCount).IsEqualTo(1);
            await Assert.That(valueCount).IsEqualTo(3);
            await Assert.That(depth).IsEqualTo(1);
            await Assert.That(removedFrame.Function).IsNull();
            await Assert.That(removedValue.AsI32()).IsEqualTo(0);
            await Assert.That(fullyRestored).IsTrue();
        }
    }
}

internal class InterpreterContext_EnterFrameTests
{
    [Test]
    public async Task 七種の追加localsを持つ関数へ入る_引数に続けて型別のゼロとnullを積みoperand開始位置を設定する()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [],
            new([WasmValueKind.ExternRef], []),
            [
                new(1, WasmValueKind.I32),
                new(2, WasmValueKind.I64),
                new(0, WasmValueKind.F64),
                new(1, WasmValueKind.F32),
                new(1, WasmValueKind.F64),
                new(1, WasmValueKind.V128),
                new(1, WasmValueKind.FuncRef),
                new(1, WasmValueKind.ExternRef),
            ],
            0
        );
        var reference = new object();
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionFrame frame;
        WasmValue[] values;

        // Act
        try
        {
            context.EnsureCapacity(1, 0, location);
            context.PushFrame(new(FunctionFixture.Create(), 0, 0));
            context.PushValue(WasmValue.FromI32(42));
            context.EnsureCapacity(10, 0, location);
            context.PushValue(WasmValue.FromExternRef(reference));
            context.EnterFrame(function, 1);
            frame = context.GetFrame(1);
            values = [.. Enumerable.Range(0, context.ValueCount).Select(context.GetValue)];
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(frame.Function).IsSameReferenceAs(function);
            await Assert.That(frame.Pc).IsEqualTo(0);
            await Assert.That(frame.StackBase).IsEqualTo(1);
            await Assert.That(frame.OperandBase).IsEqualTo(10);
            await Assert.That(values.Length).IsEqualTo(10);
            await Assert.That(values[0].AsI32()).IsEqualTo(42);
            await Assert.That(values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(values[2]).IsEqualTo(WasmValue.FromI32(0));
            await Assert.That(values[3]).IsEqualTo(WasmValue.FromI64(0));
            await Assert.That(values[4]).IsEqualTo(WasmValue.FromI64(0));
            await Assert.That(values[5]).IsEqualTo(WasmValue.FromF32Bits(0));
            await Assert.That(values[6]).IsEqualTo(WasmValue.FromF64Bits(0));
            await Assert.That(values[7]).IsEqualTo(WasmValue.FromV128(0, 0));
            await Assert.That(values[8]).IsEqualTo(WasmValue.FromFuncRef(null));
            await Assert.That(values[9]).IsEqualTo(WasmValue.FromExternRef(null));
        }
    }

    [Test]
    public async Task 同じ関数へ入れ子で入り結果を返す_内側を外側のoperandの後に置き外側の引数とlocalsを保つ()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [],
            new([WasmValueKind.I64], [WasmValueKind.F32]),
            [new(1, WasmValueKind.F32)],
            1
        );
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, 30, 0);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionFrame outerFrame;
        ExecutionFrame innerFrame;
        WasmValue innerLocal;
        WasmValue[] values;
        int frameCount;

        // Act
        try
        {
            context.EnsureCapacity(3, 1, location);
            context.PushValue(WasmValue.FromI64(7));
            context.EnterFrame(function, 0);
            context.PushValue(WasmValue.FromI64(9));
            context.EnsureCapacity(4, 1, location);
            context.TryEnterCall();
            context.EnterFrame(function, 2);
            innerFrame = context.GetFrame(1);
            innerLocal = context.GetValue(3);
            context.PushValue(WasmValue.FromF32Bits(0x7FC00001));
            context.CompleteFrame();
            outerFrame = context.GetFrame(0);
            frameCount = context.FrameCount;
            values = [.. Enumerable.Range(0, context.ValueCount).Select(context.GetValue)];
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(outerFrame.StackBase).IsEqualTo(0);
            await Assert.That(outerFrame.OperandBase).IsEqualTo(2);
            await Assert.That(innerFrame.StackBase).IsEqualTo(2);
            await Assert.That(innerFrame.OperandBase).IsEqualTo(4);
            await Assert.That(innerLocal).IsEqualTo(WasmValue.FromF32Bits(0));
            await Assert.That(frameCount).IsEqualTo(1);
            await Assert.That(values.Length).IsEqualTo(3);
            await Assert.That(values[0].AsI64()).IsEqualTo(7L);
            await Assert.That(values[1]).IsEqualTo(WasmValue.FromF32Bits(0));
            await Assert.That(values[2].AsF32Bits()).IsEqualTo(0x7FC00001U);
        }
    }
}
