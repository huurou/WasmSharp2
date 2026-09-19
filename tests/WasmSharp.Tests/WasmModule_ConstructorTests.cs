using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

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
        ModuleExport[] exports = [new("run", uint.MaxValue, 20, WasmExternalKind.Function)];
        ModuleImport[] imports = [new FunctionImport("env", "f", uint.MaxValue, 12)];
        TableDefinition[] tables = [new(WasmValueKind.FuncRef, new WasmLimits(1), 15)];
        MemoryDefinition[] memories = [new(new WasmLimits(uint.MaxValue), 18)];
        DecodedInstruction[] initializer = [new(new(0, 0x41), WasmValue.FromI32(42), 22, 0)];
        GlobalDefinition[] globals = [new(new(WasmValueKind.I32, true), initializer, 20)];

        // Act
        var module = new WasmModule(
            types,
            functions,
            exports,
            48,
            imports,
            tables,
            memories,
            globals,
            new StartDefinition(uint.MaxValue, 25)
        );
        Array.Clear(types);
        Array.Clear(functions);
        Array.Clear(exports);
        Array.Clear(imports);
        Array.Clear(tables);
        Array.Clear(memories);
        Array.Clear(globals);
        Array.Clear(initializer);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.Length).IsEqualTo(1);
            await Assert.That(ReferenceEquals(module.Types[0], type)).IsTrue();
            await Assert.That(module.Functions.Length).IsEqualTo(1);
            await Assert.That(ReferenceEquals(module.Functions[0], function)).IsTrue();
            await Assert.That(module.Exports.Length).IsEqualTo(1);
            await Assert.That(module.Exports[0].Name).IsEqualTo("run");
            await Assert.That(module.Exports[0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Exports[0].ByteOffset).IsEqualTo(20L);
            await Assert.That(module.InputLength).IsEqualTo(48L);
            await Assert
                .That(((FunctionImport)module.Imports[0]).TypeIndex)
                .IsEqualTo(uint.MaxValue);
            await Assert.That(module.Tables[0].Limits.Minimum).IsEqualTo(1u);
            await Assert.That(module.Memories[0].Limits.Minimum).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Globals[0].Initializer[0].Immediate.AsI32()).IsEqualTo(42);
            await Assert.That(module.Start).IsEqualTo(new StartDefinition(uint.MaxValue, 25));
        }
    }

    [Test]
    public async Task 空の定義を構築する_各配列が非defaultの空になる()
    {
        // Act
        var module = new WasmModule([], [], [], 8, [], [], [], [], null);
        var function = new DecodedFunction(0, 0, [], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.IsDefault).IsFalse();
            await Assert.That(module.Functions.IsDefault).IsFalse();
            await Assert.That(module.Exports.IsDefault).IsFalse();
            await Assert.That(module.Imports.IsDefault).IsFalse();
            await Assert.That(module.Tables.IsDefault).IsFalse();
            await Assert.That(module.Memories.IsDefault).IsFalse();
            await Assert.That(module.Globals.IsDefault).IsFalse();
            await Assert.That(module.Start).IsNull();
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
