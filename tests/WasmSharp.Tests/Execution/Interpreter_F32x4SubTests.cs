using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4SubTests
{
    [Test]
    [Arguments(
        0x4000000040E00000UL,
        0x008000003F800000UL,
        0x40E0000040000000UL,
        0x007FFFFF3F800001UL,
        0xC0A0000040A00000UL,
        0x00000001B4000000UL
    )]
    [Arguments(
        0x0000000080000000UL,
        0x3F8000007F800000UL,
        0x8000000000000000UL,
        0x7F8000007F800000UL,
        0x0000000080000000UL,
        0xFF8000007FC00000UL
    )]
    [Arguments(
        0x3F800000FFC12345UL,
        0x40400000FF800000UL,
        0x7F8123453F800000UL,
        0x3F8000007F800000UL,
        0x7FC000007FC00000UL,
        0x40000000FF800000UL
    )]
    public async Task 左右の異なる値と境界値のlane_operand順に同精度で減算する(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Sub, operands);

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
