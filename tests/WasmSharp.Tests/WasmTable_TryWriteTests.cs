using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TryWriteTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 末尾まで参照列を転送する_指定範囲だけを書き換え参照型と同一性を保つ(
        WasmValueKind kind
    )
    {
        // Arrange
        var reference =
            kind == WasmValueKind.FuncRef
                ? WasmModule
                    .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
                    .Validate()
                    .Instantiate([])
                    .GetFunction("run")
                : new object();
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef((WasmFunction)reference)
                : WasmValue.FromExternRef(reference);
        var empty =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);
        var values = new[] { value, empty };
        var table = new WasmTable(kind, new WasmLimits(3));
        table.Set(2, value);

        // Act
        var success = table.TryWrite(1, values);
        values[0] = empty;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(table.Count).IsEqualTo(3u);
            await Assert.That(table.Get(0)).IsEqualTo(empty);
            await Assert.That(table.Get(1).Kind).IsEqualTo(kind);
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef
                            ? table.Get(1).AsFuncRef()
                            : table.Get(1).AsExternRef(),
                        reference
                    )
                )
                .IsTrue();
            await Assert.That(table.Get(2)).IsEqualTo(empty);
        }
    }

    [Test]
    [Arguments(0u, 0u)]
    [Arguments(2u, 0u)]
    [Arguments(2u, 2u)]
    public async Task 空の参照列を領域内または末尾へ転送する_成功する(uint count, uint destination)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(count));

        // Act
        var success = table.TryWrite(destination, []);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(2u, 2)]
    [Arguments(4u, 0)]
    [Arguments(uint.MaxValue, 0)]
    [Arguments(uint.MaxValue, 1)]
    public async Task 転送先の範囲が不正である_内容を一部も変更せずfalseを返す(
        uint destination,
        int length
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(3));
        var references = new[] { new object(), new object(), new object() };
        for (uint index = 0; index < table.Count; index++)
        {
            table.Set(index, WasmValue.FromExternRef(references[index]));
        }
        var values = new WasmValue[length];
        Array.Fill(values, WasmValue.FromExternRef(null));

        // Act
        var success = table.TryWrite(destination, values);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(table.Count).IsEqualTo(3u);
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), references[index]))
                    .IsTrue();
            }
        }
    }
}
