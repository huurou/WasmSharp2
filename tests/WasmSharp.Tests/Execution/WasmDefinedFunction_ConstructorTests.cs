using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class WasmDefinedFunction_ConstructorTests
{
    [Test]
    public async Task 関数添字と定義添字が異なる_元instanceの定義と実行コードを使う()
    {
        // Arrange
        var instance = WasmModule
            .Decode(
                ConstantModuleBinary.Create(
                    [(0x7F, [0x41, 0x07, 0x0B]), (0x7E, [0x42, 0x2A, 0x0B])],
                    [("first", 0), ("second", 1)]
                )
            )
            .Validate()
            .Instantiate([]);

        // Act
        var function = new WasmDefinedFunction(instance, uint.MaxValue, 1);
        var result = function.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(function.Instance, instance)).IsTrue();
            await Assert.That(function.FunctionIndex).IsEqualTo(uint.MaxValue);
            await Assert
                .That(ReferenceEquals(function.Type, instance.GetFunction("second").Type))
                .IsTrue();
            await Assert.That(result.Values.Single().AsI64()).IsEqualTo(42L);
        }
    }
}
