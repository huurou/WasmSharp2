namespace WasmSharp.Tests;

internal class WasmFunction_TypeTests
{
    [Test]
    public async Task 複数の定義から関数実体を作る_所有モジュールの同じ添字から型を取得する()
    {
        // Arrange
        var firstType = new WasmFunctionType([WasmValueKind.I64], [WasmValueKind.F32]);
        var secondType = new WasmFunctionType([], [WasmValueKind.I32]);
        var module = new WasmModule(
            [firstType, secondType],
            [new(1, 30, [], []), new(0, 40, [], [])],
            [],
            48
        );
        var options = new WasmExecutionOptions(10);

        // Act
        var instance = new WasmInstance(module, options);
        var other = new WasmInstance(module, WasmExecutionOptions.Default);
        var function = instance.Functions[1];
        var type = function.Type;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(instance.Module, module)).IsTrue();
            await Assert.That(ReferenceEquals(instance.ExecutionOptions, options)).IsTrue();
            await Assert.That(instance.Functions.Length).IsEqualTo(2);
            await Assert.That(ReferenceEquals(function.Instance, instance)).IsTrue();
            await Assert.That(function.FunctionIndex).IsEqualTo(1U);
            await Assert.That(ReferenceEquals(type, firstType)).IsTrue();
            await Assert.That(ReferenceEquals(instance.Functions[0].Type, secondType)).IsTrue();
            await Assert.That(ReferenceEquals(function.Definition, module.Functions[1])).IsTrue();
            await Assert.That(function.Definition.BodyOffset).IsEqualTo(40L);
            await Assert.That(ReferenceEquals(function, other.Functions[1])).IsFalse();
            await Assert.That(ReferenceEquals(other.Functions[1].Type, type)).IsTrue();
        }
    }
}
