using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class ExecutionBoundary_ThrowIfFailedTests
{
    [Test]
    [Arguments(WasmProcessingStage.Invoke)]
    [Arguments(WasmProcessingStage.Instantiate)]
    public async Task CLRスタック上限の結果を受け取る_参照診断と段階を示し未計測上限をnullのまま例外にする(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var result = ExecutionResult.Exhaustion(WasmExhaustionReason.HostStackLimit, null, 3, 42);

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.HostStackLimit);
            await Assert
                .That(
                    exception.Message.StartsWith("call stack exhausted", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert.That(exception.Limit).IsNull();
            await Assert.That(exception.Location).IsEqualTo(new WasmFailureLocation(stage, 42, 3));
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    [Test]
    public async Task 正常の結果を受け取る_値の有無によらず例外を投げない()
    {
        // Arrange
        ExecutionResult[] results =
        [
            default,
            ExecutionResult.Success([]),
            ExecutionResult.Success([WasmValue.FromI32(42)]),
        ];

        // Act & Assert
        foreach (var result in results)
        {
            await Assert
                .That(() => ExecutionBoundary.ThrowIfFailed(result, WasmProcessingStage.Invoke))
                .ThrowsNothing();
        }
    }

    [Test]
    public async Task 関数外の初期化でメモリ範囲外になる_参照診断とデータセグメント位置を保ったtrap例外にする()
    {
        // Arrange
        var result = ExecutionResult.Trap(
            WasmTrapReason.MemoryOutOfBounds,
            null,
            12345678900,
            sectionId: 11
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, WasmProcessingStage.Instantiate))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "out of bounds memory access",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(WasmProcessingStage.Instantiate, 12345678900, null, 11)
                );
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    [Test]
    [Arguments(WasmProcessingStage.Invoke)]
    [Arguments(WasmProcessingStage.Instantiate)]
    public async Task Exhaustionの結果を受け取る_段階と原因と上限と元位置を保った専用例外にする(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var result = ExecutionResult.Exhaustion(
            WasmExhaustionReason.CallDepthLimit,
            7,
            3,
            12345678900
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert
                .That(
                    exception.Message.StartsWith("call stack exhausted", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert.That(exception.Limit).IsEqualTo(7);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(stage, 12345678900, 3));
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    [Test]
    [Arguments(WasmProcessingStage.Invoke, WasmTrapReason.Unreachable, "unreachable executed")]
    [Arguments(WasmProcessingStage.Instantiate, WasmTrapReason.Unreachable, "unreachable executed")]
    [Arguments(
        WasmProcessingStage.Invoke,
        WasmTrapReason.MemoryOutOfBounds,
        "out of bounds memory access"
    )]
    [Arguments(
        WasmProcessingStage.Instantiate,
        WasmTrapReason.MemoryOutOfBounds,
        "out of bounds memory access"
    )]
    public async Task 参照診断に対応するTrapの結果を受け取る_段階と原因と元位置を保ったtrap例外にする(
        WasmProcessingStage stage,
        WasmTrapReason reason,
        string expectedMessage
    )
    {
        // Arrange
        var result = ExecutionResult.Trap(reason, uint.MaxValue, 12345678900);

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(expectedMessage, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(stage, 12345678900, uint.MaxValue));
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    [Test]
    [Arguments(WasmProcessingStage.Invoke)]
    [Arguments(WasmProcessingStage.Instantiate)]
    public async Task その他のTrapの結果を受け取る_既存診断と段階と原因と元位置を保ったtrap例外にする(
        WasmProcessingStage stage
    )
    {
        // Arrange
        WasmTrapReason[] reasons =
        [
            WasmTrapReason.TableOutOfBounds,
            WasmTrapReason.IndirectCallTypeMismatch,
            WasmTrapReason.UninitializedElement,
        ];

        foreach (var reason in reasons)
        {
            // Arrange
            var result = ExecutionResult.Trap(reason, uint.MaxValue, 12345678900);

            // Act & Assert
            var exception = await Assert
                .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
                .ThrowsExactly<WasmTrapException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Reason).IsEqualTo(reason);
                await Assert
                    .That(exception.Message)
                    .IsEqualTo("Wasmの実行中にtrapが発生しました。");
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new WasmFailureLocation(stage, 12345678900, uint.MaxValue));
                await Assert.That(exception.InnerException).IsNull();
            }
        }
    }
}
