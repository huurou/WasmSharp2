using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class ExecutionBoundary_InvokeTests
{
    [Test]
    public async Task 外側の呼び出し深さが上限に達している_外側の上限で拒否し状態と参照を保つ()
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B))
            .Validate()
            .Instantiate([], new(100))
            .GetFunction("run");
        var outerRemains = false;
        (int Frames, int Values, int Depth) state = default;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var outer = WasmExecutionContext.Enter(new(1), out var isOutermost);
                try
                {
                    outer.TryEnterCall();
                    try
                    {
                        ExecutionBoundary.Invoke(function, [], WasmProcessingStage.Instantiate);
                    }
                    finally
                    {
                        outerRemains = ReferenceEquals(WasmExecutionContext.Current, outer);
                        state = (outer.FrameCount, outer.ValueCount, outer.CallDepth);
                    }
                }
                finally
                {
                    outer.Restore(0, 0, 0);
                    outer.Exit(isOutermost);
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Limit).IsEqualTo(1);
            await Assert.That(exception.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(
                        WasmProcessingStage.Instantiate,
                        function.Definition.BodyOffset,
                        0
                    )
                );
            await Assert.That(outerRemains).IsTrue();
            await Assert.That(state).IsEqualTo((0, 0, 1));
        }
    }

    [Test]
    public async Task 関数入口で保持上限を超える_再分類せず伝播し現在のコンテキストを解除する()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create([], maxOperandStack: int.MaxValue);
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    ExecutionBoundary.Invoke(function, [], WasmProcessingStage.Invoke);
                }
                finally
                {
                    cleared = WasmExecutionContext.Current is null;
                    WasmExecutionContext.Current?.Exit(true);
                }
            })
            .ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Invoke, 12345678900, 1));
            await Assert.That(exception.InnerException).IsNull();
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    public async Task 最外側の定数関数が正常終了する_結果を返し現在のコンテキストを解除する()
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B))
            .Validate()
            .Instantiate([], new(1))
            .GetFunction("run");

        // Act
        var result = ExecutionBoundary.Invoke(function, [], WasmProcessingStage.Invoke);
        var cleared = WasmExecutionContext.Current is null;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.Single().AsI32()).IsEqualTo(42);
            await Assert.That(cleared).IsTrue();
        }
    }
}
