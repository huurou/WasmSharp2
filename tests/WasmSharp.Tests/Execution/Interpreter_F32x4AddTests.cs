using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4AddTests
{
    [Test]
    [Arguments(
        0x4B8000014B800000UL,
        0x3FC0000000000001UL,
        0x3F8000003F800000UL,
        0x4010000000000001UL,
        0x4B8000024B800000UL,
        0x4070000000000002UL
    )]
    [Arguments(
        0x0000000080000000UL,
        0x7F7FFFFF7F800000UL,
        0x8000000080000000UL,
        0x7F7FFFFFFF800000UL,
        0x0000000080000000UL,
        0x7F8000007FC00000UL
    )]
    [Arguments(
        0xFFC123457FC00000UL,
        0x3F8000007F812345UL,
        0x3F8000003F800000UL,
        0xFF81234540000000UL,
        0x7FC000007FC00000UL,
        0x7FC000007FC00000UL
    )]
    public async Task 通常値と境界値とNaNのlane_同精度で加算する(
        ulong leftLow64,
        ulong leftHigh64,
        ulong rightLow64,
        ulong rightHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(leftLow64, leftHigh64),
            WasmValue.FromV128(rightLow64, rightHigh64),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Add, operands);

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
