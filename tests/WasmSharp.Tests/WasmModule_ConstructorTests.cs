using WasmSharp.Modules;

namespace WasmSharp.Tests;

internal class WasmModule_ConstructorTests
{
    [Test]
    public async Task 元の配列を変更する_型と関数とexportと入力長を独立して保持する()
    {
        // Arrange
        var type = new WasmFunctionType([], [WasmValueKind.I32]);
        var function = new DecodedFunction(0, 30, [], []);
        WasmFunctionType[] types = [type];
        DecodedFunction[] functions = [function];
        FunctionExport[] exports = [new("run", uint.MaxValue, 20)];

        // Act
        var module = new WasmModule(types, functions, exports, 48);
        Array.Clear(types);
        Array.Clear(functions);
        Array.Clear(exports);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.Length).IsEqualTo(1);
            await Assert.That(ReferenceEquals(module.Types[0], type)).IsTrue();
            await Assert.That(module.Functions.Length).IsEqualTo(1);
            await Assert.That(ReferenceEquals(module.Functions[0], function)).IsTrue();
            await Assert.That(module.Exports.Length).IsEqualTo(1);
            await Assert.That(module.Exports[0].Name).IsEqualTo("run");
            await Assert.That(module.Exports[0].FunctionIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Exports[0].ByteOffset).IsEqualTo(20L);
            await Assert.That(module.InputLength).IsEqualTo(48L);
        }
    }

    [Test]
    public async Task 空の定義を構築する_各配列が非defaultの空になる()
    {
        // Act
        var module = new WasmModule([], [], [], 8);
        var function = new DecodedFunction(0, 0, [], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.IsDefault).IsFalse();
            await Assert.That(module.Functions.IsDefault).IsFalse();
            await Assert.That(module.Exports.IsDefault).IsFalse();
            await Assert.That(function.Locals.IsDefault).IsFalse();
            await Assert.That(function.Instructions.IsDefault).IsFalse();
            await Assert.That(module.Types.Length).IsEqualTo(0);
            await Assert.That(module.Functions.Length).IsEqualTo(0);
            await Assert.That(module.Exports.Length).IsEqualTo(0);
            await Assert.That(function.Locals.Length).IsEqualTo(0);
            await Assert.That(function.Instructions.Length).IsEqualTo(0);
        }
    }
}
