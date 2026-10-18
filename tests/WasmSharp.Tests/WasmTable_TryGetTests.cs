using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TryGetTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 範囲内の参照を取得する_型別nullと末尾の参照同一性を保つ(WasmValueKind kind)
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
        var table = new WasmTable(kind, new WasmLimits(2));
        table.Set(1, value);

        // Act
        var emptySuccess = table.TryGet(0, out var empty);
        var savedSuccess = table.TryGet(1, out var saved);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(emptySuccess).IsTrue();
            await Assert.That(savedSuccess).IsTrue();
            await Assert.That(empty.Kind).IsEqualTo(kind);
            await Assert.That(saved.Kind).IsEqualTo(kind);
            await Assert
                .That(kind == WasmValueKind.FuncRef ? empty.AsFuncRef() : empty.AsExternRef())
                .IsNull();
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef ? saved.AsFuncRef() : saved.AsExternRef(),
                        kind == WasmValueKind.FuncRef ? function : external
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(0u, 0u)]
    [Arguments(2u, 2u)]
    [Arguments(2u, uint.MaxValue)]
    public async Task 範囲外を取得する_内容を変更せずfalseを返す(uint count, uint index)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(count));
        var reference = new object();
        for (uint position = 0; position < count; position++)
        {
            table.Set(position, WasmValue.FromExternRef(reference));
        }

        // Act
        var success = table.TryGet(index, out _);

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
