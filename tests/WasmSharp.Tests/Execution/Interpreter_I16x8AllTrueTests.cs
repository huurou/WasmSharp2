using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8AllTrueTests
{
    [Test]
    [Arguments(0x0000000000000000UL, 0x0000000000000000UL, 0)]
    [Arguments(0x0001000100010001UL, 0x0001000100010001UL, 1)]
    [Arguments(0x8000800080008000UL, 0x8000800080008000UL, 1)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL, 1)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0x0000FFFFFFFFFFFFUL, 0)]
    [Arguments(0xFFFFFFFFFFFF0000UL, 0xFFFFFFFFFFFFFFFFUL, 0)]
    public async Task ゼロlaneと全非ゼロlane_各laneの非ゼロ判定を返す(
        ulong low64,
        ulong high64,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8AllTrue, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
