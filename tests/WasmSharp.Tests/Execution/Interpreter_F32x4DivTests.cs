using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4DivTests
{
    [Test]
    [Arguments(
        0x3F80000040E00000UL,
        0x8000000100000003UL,
        0x4040000040000000UL,
        0x4000000040000000UL,
        0x3EAAAAAB40600000UL,
        0x8000000000000002UL
    )]
    [Arguments(
        0x3F8000003F800000UL,
        0x80000000BF800000UL,
        0x8000000000000000UL,
        0xC00000007F800000UL,
        0xFF8000007F800000UL,
        0x0000000080000000UL
    )]
    [Arguments(
        0x7F80000000000000UL,
        0x3F800000FFC12345UL,
        0x7F80000000000000UL,
        0x7F8123453F800000UL,
        0x7FC000007FC00000UL,
        0x7FC000007FC00000UL
    )]
    [Arguments(
        0x00800000FF800000UL,
        0x8000000000000000UL,
        0x40000000C0000000UL,
        0x40000000C0000000UL,
        0x004000007F800000UL,
        0x8000000080000000UL
    )]
    public async Task 左右の異なる値と境界値のlane_operand順に同精度で除算する(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Div, operands);

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
