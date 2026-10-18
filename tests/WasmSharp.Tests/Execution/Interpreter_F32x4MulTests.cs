using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4MulTests
{
    [Test]
    [Arguments(
        0x3F80000140400000UL,
        0x0000000300800000UL,
        0x3F80000140000000UL,
        0x3F0000003F000000UL,
        0x3F80000240C00000UL,
        0x0000000200400000UL
    )]
    [Arguments(
        0x8000000080000001UL,
        0x7F80000000000000UL,
        0xC00000003F000000UL,
        0x00000000C0000000UL,
        0x0000000080000000UL,
        0x7FC0000080000000UL
    )]
    [Arguments(
        0xFF8000007F7FFFFFUL,
        0x3F800000FFC12345UL,
        0xC000000040000000UL,
        0x7F81234540400000UL,
        0x7F8000007F800000UL,
        0x7FC000007FC00000UL
    )]
    public async Task 通常値と境界値とNaNのlane_同精度で乗算する(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Mul, operands);

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
