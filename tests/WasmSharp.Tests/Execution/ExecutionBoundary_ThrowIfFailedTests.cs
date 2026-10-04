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
    [Arguments(WasmProcessingStage.Invoke)]
    [Arguments(WasmProcessingStage.Instantiate)]
    public async Task Unreachableの結果を受け取る_参照診断と段階と原因と元位置を保ったtrap例外にする(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var result = ExecutionResult.Trap(WasmTrapReason.Unreachable, uint.MaxValue, 12345678900);

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
            await Assert
                .That(
                    exception.Message.StartsWith("unreachable executed", StringComparison.Ordinal)
                )
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
            WasmTrapReason.IntegerDivideByZero,
            WasmTrapReason.IntegerOverflow,
            WasmTrapReason.InvalidConversionToInteger,
            WasmTrapReason.MemoryOutOfBounds,
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
