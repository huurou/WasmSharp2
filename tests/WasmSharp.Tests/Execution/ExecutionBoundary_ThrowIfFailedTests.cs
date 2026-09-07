using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class ExecutionBoundary_ThrowIfFailedTests
{
    [Test]
    public async Task 正常の結果を受け取る_値の有無によらず例外を投げない()
    {
        // Arrange
        ExecutionResult[] results =
        [
            default,
            ExecutionResult.Success(),
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
    public async Task Trapの結果を受け取る_段階と原因と元位置を保ったtrap例外にする(
        WasmProcessingStage stage
    )
    {
        // Arrange
        var result = ExecutionResult.Trap(
            WasmTrapReason.IntegerDivideByZero,
            uint.MaxValue,
            12345678900
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ExecutionBoundary.ThrowIfFailed(result, stage))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.IntegerDivideByZero);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(stage, 12345678900, uint.MaxValue));
            await Assert.That(exception.InnerException).IsNull();
        }
    }
}
