using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class ElementInstance_TryInitializeTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 入力配列の変更後に転送する_所有した指定範囲だけを書き込み参照型と同一性を保つ(
        WasmValueKind kind
    )
    {
        // Arrange
        var reference =
            kind == WasmValueKind.FuncRef
                ? WasmFunction.CreateHost(new WasmFunctionType([], []), _ => new([]))
                : new object();
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef((WasmFunction)reference)
                : WasmValue.FromExternRef(reference);
        var empty =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);
        var values = new[] { empty, value, empty };
        var element = new ElementInstance(values);
        var table = new WasmTable(kind, new WasmLimits(4));
        table.Set(0, value);
        table.Set(3, value);
        values[1] = empty;

        // Act
        var success = element.TryInitialize(table, 2, 1, 2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(element.Length).IsEqualTo(3u);
            await Assert.That(table.Get(0)).IsEqualTo(value);
            await Assert.That(table.Get(1)).IsEqualTo(empty);
            await Assert.That(table.Get(2).Kind).IsEqualTo(kind);
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef
                            ? table.Get(2).AsFuncRef()
                            : table.Get(2).AsExternRef(),
                        reference
                    )
                )
                .IsTrue();
            await Assert.That(table.Get(3)).IsEqualTo(empty);
        }
    }

    [Test]
    public async Task 転送後にtableを書き換えて再転送する_供給元の参照列を保持している()
    {
        // Arrange
        var first = new object();
        var second = new object();
        var element = new ElementInstance([
            WasmValue.FromExternRef(first),
            WasmValue.FromExternRef(second),
        ]);
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(4));

        // Act
        var firstSuccess = element.TryInitialize(table, 0, 0, 2);
        table.Set(0, WasmValue.FromExternRef(null));
        var secondSuccess = element.TryInitialize(table, 2, 0, 2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstSuccess).IsTrue();
            await Assert.That(secondSuccess).IsTrue();
            await Assert.That(element.Length).IsEqualTo(2u);
            await Assert.That(table.Get(0).AsExternRef()).IsNull();
            await Assert.That(ReferenceEquals(table.Get(2).AsExternRef(), first)).IsTrue();
            await Assert.That(ReferenceEquals(table.Get(3).AsExternRef(), second)).IsTrue();
        }
    }

    [Test]
    [Arguments(0u, 0u, 0u)]
    [Arguments(0u, 0u, 2u)]
    [Arguments(3u, 0u, 0u)]
    [Arguments(3u, 3u, 0u)]
    [Arguments(3u, 0u, 2u)]
    [Arguments(3u, 3u, 2u)]
    public async Task 長さがゼロで両開始位置が領域内または末尾にある_成功する(
        uint count,
        uint destination,
        uint source
    )
    {
        // Arrange
        var element = new ElementInstance([
            WasmValue.FromExternRef(new object()),
            WasmValue.FromExternRef(null),
        ]);
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(count));

        // Act
        var success = element.TryInitialize(table, destination, source, 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(0u, 1u, 2u)]
    [Arguments(2u, 0u, 2u)]
    [Arguments(2u, 1u, 2u)]
    [Arguments(0u, 3u, 0u)]
    [Arguments(4u, 0u, 0u)]
    [Arguments(0u, uint.MaxValue, 0u)]
    [Arguments(0u, uint.MaxValue, 2u)]
    [Arguments(uint.MaxValue, 0u, 0u)]
    [Arguments(uint.MaxValue, 0u, 2u)]
    [Arguments(0u, 1u, uint.MaxValue)]
    [Arguments(1u, 0u, uint.MaxValue)]
    public async Task いずれかの範囲が領域外にある_内容を一部も変更せずfalseを返す(
        uint destination,
        uint source,
        uint length
    )
    {
        // Arrange
        var element = new ElementInstance([
            WasmValue.FromExternRef(new object()),
            WasmValue.FromExternRef(null),
        ]);
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(3));
        var references = new[] { new object(), new object(), new object() };
        for (uint index = 0; index < table.Count; index++)
        {
            table.Set(index, WasmValue.FromExternRef(references[index]));
        }

        // Act
        var success = element.TryInitialize(table, destination, source, length);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(element.Length).IsEqualTo(2u);
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), references[index]))
                    .IsTrue();
            }
        }
    }

    [Test]
    [Arguments(0u, 0u, 0u, true)]
    [Arguments(3u, 0u, 0u, true)]
    [Arguments(4u, 0u, 0u, false)]
    [Arguments(0u, 1u, 0u, false)]
    [Arguments(0u, 0u, 1u, false)]
    public async Task 破棄済みである_コピー元位置と長さがゼロかつコピー先が範囲内の場合だけ成功する(
        uint destination,
        uint source,
        uint length,
        bool expectedSuccess
    )
    {
        // Arrange
        var element = new ElementInstance([WasmValue.FromExternRef(new object())]);
        element.Drop();
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(3));
        var reference = new object();
        table.TryFill(0, WasmValue.FromExternRef(reference), 3);

        // Act
        var success = element.TryInitialize(table, destination, source, length);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsEqualTo(expectedSuccess);
            await Assert.That(element.Length).IsEqualTo(0u);
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), reference))
                    .IsTrue();
            }
        }
    }
}
