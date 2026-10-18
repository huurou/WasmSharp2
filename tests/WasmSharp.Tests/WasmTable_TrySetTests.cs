using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TrySetTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 末尾に非null参照とnullを設定する_同じ実体の公開操作へ反映する(
        WasmValueKind kind
    )
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate()
            .Instantiate([])
            .GetFunction("run");
        var external = new object();
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(function)
                : WasmValue.FromExternRef(external);
        var empty =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);
        var table = new WasmTable(kind, new WasmLimits(2));
        var alias = table;

        // Act
        var valueSuccess = alias.TrySet(1, value);
        var saved = table.Get(1);
        var emptySuccess = alias.TrySet(1, empty);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(valueSuccess).IsTrue();
            await Assert.That(emptySuccess).IsTrue();
            await Assert.That(saved.Kind).IsEqualTo(kind);
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef ? saved.AsFuncRef() : saved.AsExternRef(),
                        kind == WasmValueKind.FuncRef ? function : external
                    )
                )
                .IsTrue();
            await Assert.That(table.Get(0)).IsEqualTo(empty);
            await Assert.That(table.Get(1)).IsEqualTo(empty);
        }
    }

    [Test]
    [Arguments(0u, 0u)]
    [Arguments(2u, 2u)]
    [Arguments(2u, uint.MaxValue)]
    public async Task 範囲外へ設定する_内容を変更せずfalseを返す(uint count, uint index)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(count));
        var reference = new object();
        for (uint position = 0; position < count; position++)
        {
            table.Set(position, WasmValue.FromExternRef(reference));
        }

        // Act
        var success = table.TrySet(index, WasmValue.FromExternRef(null));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(table.Count).IsEqualTo(count);
            for (uint position = 0; position < count; position++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(position).AsExternRef(), reference))
                    .IsTrue();
            }
        }
    }
}
