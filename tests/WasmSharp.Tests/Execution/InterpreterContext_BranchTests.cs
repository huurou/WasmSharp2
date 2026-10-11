using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class InterpreterContext_GetBranchTargetTests
{
    [Test]
    [Arguments(0U)]
    [Arguments(1U)]
    public async Task 入れ子の関数で分岐情報を取得する_現在の関数の指定添字を参照する(uint index)
    {
        // Arrange
        BranchTarget[] targets = [new(1, 0, 1), new(0, 2, 0)];
        var outer = ExecutionFunctionFixture.Create([], new([], []), [], 0, [new(9, 8, 7)]);
        var inner = ExecutionFunctionFixture.Create(
            [new(ExecutionOpcode.Op0B, default, 81, 0), new(ExecutionOpcode.Op0B, default, 82, 0)],
            new([], []),
            [],
            3,
            [.. targets]
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        BranchTarget actual;
        ExecutionFrame frame;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            context.EnsureCapacity(0, 0, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(new(outer, 0, 0) { Pc = 5 });
            context.EnsureCapacity(0, 0, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(new(inner, 0, 0) { Pc = 1 });

            actual = context.GetBranchTarget(index);
            frame = context.GetFrame(1);
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
            await Assert.That(actual).IsEqualTo(targets[index]);
            await Assert.That(frame.Function).IsSameReferenceAs(inner);
            await Assert.That(frame.Pc).IsEqualTo(1);
            await Assert.That(state).IsEqualTo((2, 0, 2));
        }
    }
}

internal class InterpreterContext_BranchTests
{
    [Test]
    [Arguments(0, 2)]
    [Arguments(1, 2)]
    [Arguments(11, 1)]
    [Arguments(11, 11)]
    public async Task 末尾の値を保持して分岐する_順序と全値型を保ち破棄範囲を解除して現在の値数とPcだけを変える(
        int keepCount,
        int discardCount
    )
    {
        // Arrange
        var reference = new object();
        var referencedFunction = FunctionFixture.Create();
        WasmValue[] values =
        [
            WasmValue.FromI32(unchecked((int)0xFEDCBA98)),
            WasmValue.FromI64(unchecked((long)0xFEDCBA9876543210)),
            WasmValue.FromF32Bits(0xFFA12345),
            WasmValue.FromF32Bits(0x80000000),
            WasmValue.FromF64Bits(0xFFF0123456789ABC),
            WasmValue.FromF64Bits(0x8000000000000000),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
            WasmValue.FromFuncRef(referencedFunction),
            WasmValue.FromExternRef(reference),
        ];
        var expected = values.TakeLast(keepCount).ToArray();
        WasmValue[] prefix =
        [
            WasmValue.FromI64(73),
            WasmValue.FromFuncRef(referencedFunction),
            WasmValue.FromExternRef(new object()),
            WasmValue.FromExternRef(new object()),
            WasmValue.FromF32Bits(0x80000000),
            WasmValue.FromV128(17, 19),
        ];
        var target = new BranchTarget(2, 1, keepCount);
        var function = ExecutionFunctionFixture.Create(
            [
                new(ExecutionOpcode.Op41, WasmValue.FromI32(1), 81, 0),
                new(ExecutionOpcode.Op41, WasmValue.FromI32(2), 82, 0),
                new(ExecutionOpcode.Op0B, default, 83, 0),
            ],
            new([WasmValueKind.ExternRef], []),
            [new(1, WasmValueKind.F32)],
            1 + discardCount + keepCount,
            [target]
        );
        var caller = new ExecutionFrame(FunctionFixture.Create(), 0, 2) { Pc = 7 };
        var current = new ExecutionFrame(function, 3, 5) { Pc = 1 };
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        WasmValue[] actualPrefix;
        WasmValue[] actualKept;
        WasmValue[] cleared;
        ExecutionFrame actualCaller;
        ExecutionFrame actualCurrent;
        (int Frames, int Values, int Depth) state;
        bool restored;

        // Act
        try
        {
            var oldEnd = prefix.Length + discardCount + keepCount;
            context.EnsureCapacity(0, oldEnd, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(caller);
            foreach (var value in prefix)
            {
                context.PushValue(value);
            }
            context.EnsureCapacity(0, oldEnd, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(current);
            for (var i = 0; i < discardCount; i++)
            {
                context.PushValue(
                    i % 2 == 0
                        ? WasmValue.FromExternRef(new object())
                        : WasmValue.FromFuncRef(FunctionFixture.Create())
                );
            }
            foreach (var value in expected)
            {
                context.PushValue(value);
            }

            context.Branch(context.GetBranchTarget(0));

            actualPrefix = [.. Enumerable.Range(0, prefix.Length).Select(context.GetValue)];
            actualKept = [.. Enumerable.Range(prefix.Length, keepCount).Select(context.GetValue)];
            cleared =
            [
                .. Enumerable
                    .Range(prefix.Length + keepCount, discardCount)
                    .Select(context.GetValue),
            ];
            actualCaller = context.GetFrame(0);
            actualCurrent = context.GetFrame(1);
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
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

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actualCaller).IsEqualTo(caller);
            await Assert.That(actualCurrent.Function).IsSameReferenceAs(function);
            await Assert.That(actualCurrent.StackBase).IsEqualTo(current.StackBase);
            await Assert.That(actualCurrent.OperandBase).IsEqualTo(current.OperandBase);
            await Assert.That(actualCurrent.Pc).IsEqualTo(target.TargetPc);
            await Assert.That(state).IsEqualTo((2, prefix.Length + keepCount, 2));
            await Assert.That(restored).IsTrue();
            for (var i = 0; i < prefix.Length; i++)
            {
                await Assert.That(actualPrefix[i]).IsEqualTo(prefix[i]);
            }
            for (var i = 0; i < keepCount; i++)
            {
                await Assert.That(actualKept[i].Kind).IsEqualTo(expected[i].Kind);
                await Assert.That(actualKept[i]).IsEqualTo(expected[i]);
                if (expected[i].Kind == WasmValueKind.FuncRef)
                {
                    await Assert
                        .That(actualKept[i].AsFuncRef())
                        .IsSameReferenceAs(expected[i].AsFuncRef());
                }
                if (expected[i].Kind == WasmValueKind.ExternRef)
                {
                    await Assert
                        .That(actualKept[i].AsExternRef())
                        .IsSameReferenceAs(expected[i].AsExternRef());
                }
            }
            foreach (var value in cleared)
            {
                await Assert.That(value).IsEqualTo(default(WasmValue));
            }
        }
    }
}
