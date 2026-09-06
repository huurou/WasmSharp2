using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

public class ExecutionResult_ValuesTests
{
    [Test]
    public async Task 既定値と空の正常結果と失敗_列挙可能な空配列を返す()
    {
        // Arrange
        ExecutionResult[] results =
        [
            default,
            ExecutionResult.Success(default),
            ExecutionResult.Success([]),
            ExecutionResult.Trap(WasmTrapReason.Unreachable, 2, 37),
            ExecutionResult.Exhaustion(WasmExhaustionReason.CallDepthLimit, 10, 3, 42),
        ];

        // Act
        var values = results.Select(x => x.Values).ToArray();

        // Assert
        foreach (var actual in values)
        {
            using (Assert.Multiple())
            {
                await Assert.That(actual.IsDefault).IsFalse();
                await Assert.That(actual.Length).IsEqualTo(0);
                await Assert.That(actual.ToArray().Length).IsEqualTo(0);
            }
        }
        await Assert.That(results[0].Status).IsEqualTo(ExecutionStatus.Success);
    }
}

public class ExecutionResult_SuccessTests
{
    [Test]
    public async Task 複数の戻り値を指定する_型と順序とビット列を保持する()
    {
        // Arrange
        ImmutableArray<WasmValue> values =
        [
            WasmValue.FromI32(-7),
            WasmValue.FromF64Bits(0xFFF8123456789ABCUL),
        ];

        // Act
        var result = ExecutionResult.Success(values);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(2);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(-7);
            await Assert.That(result.Values[1].AsF64Bits()).IsEqualTo(0xFFF8123456789ABCUL);
            await Assert.That(result.TrapReason).IsNull();
            await Assert.That(result.ExhaustionReason).IsNull();
            await Assert.That(result.Limit).IsNull();
            await Assert.That(result.FunctionIndex).IsNull();
            await Assert.That(result.ByteOffset).IsNull();
        }
    }
}

public class ExecutionResult_TrapTests
{
    [Test]
    public async Task trapを構築する_原因と元位置だけを保持する()
    {
        // Arrange
        const uint FUNCTION_INDEX = uint.MaxValue;

        // Act
        var result = ExecutionResult.Trap(WasmTrapReason.IntegerDivideByZero, FUNCTION_INDEX, 37);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(result.TrapReason).IsEqualTo(WasmTrapReason.IntegerDivideByZero);
            await Assert.That(result.FunctionIndex).IsEqualTo(FUNCTION_INDEX);
            await Assert.That(result.ByteOffset).IsEqualTo(37L);
            await Assert.That(result.ExhaustionReason).IsNull();
            await Assert.That(result.Limit).IsNull();
        }
    }
}

public class ExecutionResult_ExhaustionTests
{
    [Test]
    public async Task exhaustionを構築する_原因と適用上限と元位置を保持する()
    {
        // Arrange
        const int LIMIT = 100;

        // Act
        var result = ExecutionResult.Exhaustion(WasmExhaustionReason.CallDepthLimit, LIMIT, 3, 42);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Exhaustion);
            await Assert
                .That(result.ExhaustionReason)
                .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(result.Limit).IsEqualTo(LIMIT);
            await Assert.That(result.FunctionIndex).IsEqualTo(3U);
            await Assert.That(result.ByteOffset).IsEqualTo(42L);
            await Assert.That(result.TrapReason).IsNull();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task 正でない上限を指定する_引数不正として拒否する(int limit)
    {
        // Act & Assert
        await Assert
            .That(() =>
                ExecutionResult.Exhaustion(WasmExhaustionReason.CallDepthLimit, limit, 0, 0)
            )
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
