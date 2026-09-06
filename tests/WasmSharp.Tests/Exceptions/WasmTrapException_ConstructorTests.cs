using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Exceptions;

public class WasmTrapException_ConstructorTests
{
    [Test]
    public async Task 既存の構築経路を使う_原因と位置は未指定のまま既存の情報を保持する()
    {
        // Arrange
        var innerException = new InvalidOperationException("原因");

        // Act
        WasmTrapException[] exceptions =
        [
            new(),
            new("trap"),
            new("trap", innerException),
            new(null),
            new(null, null),
        ];

        // Assert
        using (Assert.Multiple())
        {
            foreach (var exception in exceptions)
            {
                await Assert.That(exception.Reason).IsNull();
                await Assert.That(exception.Location).IsNull();
            }
            await Assert.That(exceptions[1].Message).IsEqualTo("trap");
            await Assert.That(exceptions[2].Message).IsEqualTo("trap");
            await Assert
                .That(ReferenceEquals(exceptions[2].InnerException, innerException))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(WasmTrapReason.Unreachable, WasmProcessingStage.Invoke)]
    [Arguments(WasmTrapReason.IntegerDivideByZero, WasmProcessingStage.Invoke)]
    [Arguments(WasmTrapReason.IntegerOverflow, WasmProcessingStage.Invoke)]
    [Arguments(WasmTrapReason.InvalidConversionToInteger, WasmProcessingStage.Invoke)]
    [Arguments(WasmTrapReason.MemoryOutOfBounds, WasmProcessingStage.Instantiate)]
    [Arguments(WasmTrapReason.TableOutOfBounds, WasmProcessingStage.Instantiate)]
    [Arguments(WasmTrapReason.IndirectCallTypeMismatch, WasmProcessingStage.Instantiate)]
    [Arguments(WasmTrapReason.UninitializedElement, WasmProcessingStage.Instantiate)]
    public async Task trapの診断を指定する_原因と発生段階と入力位置を保持する(
        WasmTrapReason reason,
        WasmProcessingStage stage
    )
    {
        // Arrange
        var location = new WasmFailureLocation(stage, 42, 3, 10);
        var innerException = new InvalidOperationException("原因");

        // Act
        var exception = new WasmTrapException("実行が中断した", reason, location, innerException);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Reason).IsEqualTo(reason);
            await Assert.That(exception.GetType().BaseType).IsEqualTo(typeof(WasmException));
            await Assert.That(exception.Location).IsEqualTo(location);
            await Assert.That(exception.Message).IsEqualTo("実行が中断した");
            await Assert.That(ReferenceEquals(exception.InnerException, innerException)).IsTrue();
        }
    }
}
