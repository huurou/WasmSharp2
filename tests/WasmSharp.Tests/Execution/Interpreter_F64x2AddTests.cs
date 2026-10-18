using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2AddTests
{
    [Test]
    [Arguments(
        0x4340000000000000UL,
        0x4340000000000001UL,
        0x3FF0000000000000UL,
        0x3FF0000000000000UL,
        0x4340000000000000UL,
        0x4340000000000002UL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x3FF8000000000000UL,
        0x0000000000000001UL,
        0x4002000000000000UL,
        0x0000000000000002UL,
        0x400E000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x0000000000000000UL,
        0x8000000000000000UL,
        0x8000000000000000UL,
        0x8000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF0000000000000UL,
        0x7FEFFFFFFFFFFFFFUL,
        0xFFF0000000000000UL,
        0x7FEFFFFFFFFFFFFFUL,
        0x7FF8000000000000UL,
        0x7FF0000000000000UL
    )]
    [Arguments(
        0x7FF8000000000000UL,
        0xFFF8123456789ABCUL,
        0x3FF0000000000000UL,
        0x3FF0000000000000UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7FF0123456789ABCUL,
        0x3FF0000000000000UL,
        0x4000000000000000UL,
        0xFFF0123456789ABCUL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2Add, operands);

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
