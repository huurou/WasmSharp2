using System.Collections.Immutable;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ConstantInitializer_EvaluateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 検証済みの定数またはglobal取得式を渡す_I32の値を保持して評価する(
        bool useGlobal
    )
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            useGlobal
                ? DecodedInstruction.WithIndex(new(0, 0x23), 1, 40)
                : DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(-1), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];
        WasmGlobal[] globals =
        [
            new(new(WasmValueKind.I32, false), WasmValue.FromI32(0)),
            new(new(WasmValueKind.I32, false), WasmValue.FromI32(-1)),
        ];

        // Act
        var result = ConstantInitializer.Evaluate(expression, globals);

        // Assert
        await Assert.That(result.AsI32()).IsEqualTo(-1);
    }
}
