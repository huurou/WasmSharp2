using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8ExtractLaneUTests
{
    [Test]
    [Arguments(0, 0x7766554433228000UL, 0xFFFFDDCCBBAA9988UL, 32768)]
    [Arguments(7, 0x7766554433228000UL, 0xFFFFDDCCBBAA9988UL, 65535)]
    [Arguments(7, 0x7766554433228000UL, 0x7FFFDDCCBBAA9988UL, 32767)]
    public async Task 最初と最後のlaneの境界bit_指定スカラー型で取り出す(
        byte laneIndex,
        ulong low64,
        ulong high64,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8ExtractLaneU, operands, laneIndex);

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
