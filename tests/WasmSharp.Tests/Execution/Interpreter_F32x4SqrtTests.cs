using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4SqrtTests
{
    [Test]
    [Arguments(
        0x4000000040800000UL,
        0x8000000000000001UL,
        0x3FB504F340000000UL,
        0x800000001A3504F3UL
    )]
    [Arguments(
        0x7F80000000000000UL,
        0xFF800000BF800000UL,
        0x7F80000000000000UL,
        0x7FC000007FC00000UL
    )]
    [Arguments(
        0x7F8123457FC00000UL,
        0x41100000FFC12345UL,
        0x7FC000007FC00000UL,
        0x404000007FC00000UL
    )]
    public async Task 正負の値と境界値とNaNのlane_同精度で平方根を返す(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Sqrt, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.Value.AsV128()).IsEqualTo((expectedLow64, expectedHigh64));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
