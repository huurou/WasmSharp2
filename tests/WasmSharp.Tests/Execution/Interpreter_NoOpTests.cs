using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_NoOpTests
{
    [Test]
    public async Task 値とframeを保持している_値とPcと観測状態を変更しない()
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromI32(-1),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFF812345),
            WasmValue.FromF64Bits(0x8000000000000000),
        ];
        var instruction = new Instruction(default, default, 81, 0);

        // Act
        var actual = ControlHandlerFixture.Execute(Interpreter.NoOp, in instruction, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.After).IsEqualTo(actual.Before);
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(actual.Values.Length).IsEqualTo(operands.Length + 2);
            await Assert.That(actual.Values[0]).IsEqualTo(ControlHandlerFixture.Local);
            await Assert.That(actual.Values[1]).IsEqualTo(ControlHandlerFixture.Sentinel);
            for (var i = 0; i < operands.Length; i++)
            {
                await Assert.That(actual.Values[i + 2]).IsEqualTo(operands[i]);
            }
        }
    }
}
