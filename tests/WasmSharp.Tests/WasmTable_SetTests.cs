using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_SetTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 非null参照とnullを設定する_参照同一性と他要素を保持する(WasmValueKind kind)
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
        alias.Set(1, value);
        var saved = table.Get(1);
        alias.Set(1, empty);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved.Kind).IsEqualTo(kind);
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef ? saved.AsFuncRef() : saved.AsExternRef(),
                        kind == WasmValueKind.FuncRef ? function : external
                    )
                )
                .IsTrue();
            await Assert.That(table.Get(1)).IsEqualTo(empty);
            await Assert.That(table.Get(0)).IsEqualTo(empty);
        }
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 要素型と異なる値を設定する_元の要素を変更せず拒否する(
        WasmValueKind kind,
        bool numeric
    )
    {
        // Arrange
        var table = new WasmTable(kind, new WasmLimits(1));
        var original = table.Get(0);
        var invalid =
            numeric ? WasmValue.FromI32(1)
            : kind == WasmValueKind.FuncRef ? WasmValue.FromExternRef(null)
            : WasmValue.FromFuncRef(null);

        // Act & Assert
        await Assert.That(() => table.Set(0, invalid)).ThrowsExactly<ArgumentException>();
        await Assert.That(table.Get(0)).IsEqualTo(original);
    }

    [Test]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    public async Task 範囲外へ設定する_元の要素を変更せず拒否する(uint index)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1));
        var reference = new object();
        table.Set(0, WasmValue.FromExternRef(reference));

        // Act & Assert
        await Assert
            .That(() => table.Set(index, WasmValue.FromExternRef(null)))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
    }
}
