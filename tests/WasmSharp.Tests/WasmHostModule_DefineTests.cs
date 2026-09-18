using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmHostModule_DefineTests
{
    [Test]
    [Arguments(WasmExternalKind.Function, false)]
    [Arguments(WasmExternalKind.Global, false)]
    [Arguments(WasmExternalKind.Memory, false)]
    [Arguments(WasmExternalKind.Table, false)]
    [Arguments(WasmExternalKind.Function, true)]
    [Arguments(WasmExternalKind.Global, true)]
    [Arguments(WasmExternalKind.Memory, true)]
    [Arguments(WasmExternalKind.Table, true)]
    public async Task 名前または実体がnull_既存定義を保って拒否する(
        WasmExternalKind kind,
        bool nullName
    )
    {
        // Arrange
        var module = new WasmHostModule();
        var original = FunctionFixture.Create();
        module.Define("keep", original);

        // Act & Assert
        var exception = await Assert
            .That(() => Define(module, nullName ? null! : "new", kind, !nullName))
            .ThrowsExactly<ArgumentNullException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.ParamName)
                .IsEqualTo(nullName ? "name" : kind.ToString().ToLowerInvariant());
            await Assert.That(module.Snapshot().Count).IsEqualTo(1);
            await Assert
                .That(
                    module.Snapshot()["keep"] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, original)
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(WasmExternalKind.Function)]
    [Arguments(WasmExternalKind.Global)]
    [Arguments(WasmExternalKind.Memory)]
    [Arguments(WasmExternalKind.Table)]
    public async Task 種類を問わず名前が重複する_既存の実体を保って拒否する(WasmExternalKind kind)
    {
        // Arrange
        var module = new WasmHostModule();
        var original = FunctionFixture.Create();
        module.Define("same", original);

        // Act & Assert
        await Assert.That(() => Define(module, "same", kind)).ThrowsExactly<ArgumentException>();
        using (Assert.Multiple())
        {
            await Assert.That(module.Snapshot().Count).IsEqualTo(1);
            await Assert
                .That(
                    module.Snapshot()["same"] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, original)
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 空名や大文字小文字の異なる名前で4種を定義する_同じ実体を区別して保持する()
    {
        // Arrange
        var module = new WasmHostModule("env");
        var function = FunctionFixture.Create();
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var memory = new WasmMemory(new(0, 1));
        var table = new WasmTable(WasmValueKind.FuncRef, new(0, 1));

        // Act
        module.Define("", function);
        module.Define("value", global);
        module.Define("Value", memory);
        module.Define("table", table);
        var items = module.Snapshot();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Name).IsEqualTo("env");
            await Assert.That(items.Count).IsEqualTo(4);
            await Assert
                .That(
                    items[""] is WasmExternalValue.Function f && ReferenceEquals(f.Value, function)
                )
                .IsTrue();
            await Assert
                .That(
                    items["value"] is WasmExternalValue.Global g && ReferenceEquals(g.Value, global)
                )
                .IsTrue();
            await Assert
                .That(
                    items["Value"] is WasmExternalValue.Memory m && ReferenceEquals(m.Value, memory)
                )
                .IsTrue();
            await Assert
                .That(
                    items["table"] is WasmExternalValue.Table t && ReferenceEquals(t.Value, table)
                )
                .IsTrue();
        }
    }

    private static void Define(
        WasmHostModule module,
        string name,
        WasmExternalKind kind,
        bool nullValue = false
    )
    {
        switch (kind)
        {
            case WasmExternalKind.Function:
                module.Define(name, nullValue ? (WasmFunction)null! : FunctionFixture.Create());
                break;
            case WasmExternalKind.Global:
                module.Define(
                    name,
                    nullValue
                        ? (WasmGlobal)null!
                        : new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(1))
                );
                break;
            case WasmExternalKind.Memory:
                module.Define(name, nullValue ? (WasmMemory)null! : new WasmMemory(new(0)));
                break;
            case WasmExternalKind.Table:
                module.Define(
                    name,
                    nullValue ? (WasmTable)null! : new WasmTable(WasmValueKind.FuncRef, new(0))
                );
                break;
        }
    }
}
