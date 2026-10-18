using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TryCopyTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 別tableから双方の末尾までコピーする_供給元と範囲外を保ち参照同一性を転送する(
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
        var source = new WasmTable(kind, new WasmLimits(3));
        source.Set(1, value);
        var destination = new WasmTable(kind, new WasmLimits(4));
        destination.Set(0, value);
        destination.Set(3, value);

        // Act
        var success = destination.TryCopy(source, 2, 1, 2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(destination.Count).IsEqualTo(4u);
            await Assert.That(source.Count).IsEqualTo(3u);
            await Assert.That(destination.Get(0)).IsEqualTo(value);
            await Assert.That(destination.Get(1)).IsEqualTo(empty);
            await Assert.That(destination.Get(2).Kind).IsEqualTo(kind);
            await Assert
                .That(
                    ReferenceEquals(
                        kind == WasmValueKind.FuncRef
                            ? destination.Get(2).AsFuncRef()
                            : destination.Get(2).AsExternRef(),
                        reference
                    )
                )
                .IsTrue();
            await Assert.That(destination.Get(3)).IsEqualTo(empty);
            await Assert.That(source.Get(0)).IsEqualTo(empty);
            await Assert.That(source.Get(1)).IsEqualTo(value);
            await Assert.That(source.Get(2)).IsEqualTo(empty);
        }
    }

    [Test]
    [Arguments(1u, 0u)]
    [Arguments(0u, 1u)]
    public async Task 同一実体の別名を使い前後に重なる範囲をコピーする_コピー前の参照列を転送する(
        uint destination,
        uint sourceOffset
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(4));
        var references = new[] { new object(), new object(), new object(), new object() };
        for (uint index = 0; index < table.Count; index++)
        {
            table.Set(index, WasmValue.FromExternRef(references[index]));
        }
        var alias = table;
        var expected =
            destination > sourceOffset
                ? new[] { references[0], references[0], references[1], references[2] }
                : new[] { references[1], references[2], references[3], references[3] };

        // Act
        var success = table.TryCopy(alias, destination, sourceOffset, 3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), expected[index]))
                    .IsTrue();
            }
        }
    }

    [Test]
    [Arguments(0u, 0u, 0u, 0u)]
    [Arguments(2u, 3u, 2u, 3u)]
    [Arguments(2u, 3u, 1u, 1u)]
    public async Task 長さがゼロで双方の開始位置が領域内または末尾にある_成功する(
        uint destinationCount,
        uint sourceCount,
        uint destination,
        uint sourceOffset
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(destinationCount));
        var source = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(sourceCount));

        // Act
        var success = table.TryCopy(source, destination, sourceOffset, 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(2u, 0u, 2u)]
    [Arguments(0u, 3u, 2u)]
    [Arguments(4u, 0u, 0u)]
    [Arguments(0u, 5u, 0u)]
    [Arguments(uint.MaxValue, 0u, 1u)]
    [Arguments(0u, uint.MaxValue, 1u)]
    [Arguments(1u, 0u, uint.MaxValue)]
    [Arguments(0u, 1u, uint.MaxValue)]
    public async Task コピー元または先の範囲が不正である_双方の内容を一部も変更せずfalseを返す(
        uint destination,
        uint sourceOffset,
        uint length
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(3));
        var source = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(4));
        var references = new[] { new object(), new object(), new object(), new object() };
        for (uint index = 0; index < source.Count; index++)
        {
            source.Set(index, WasmValue.FromExternRef(references[index]));
        }
        for (uint index = 0; index < table.Count; index++)
        {
            table.Set(index, WasmValue.FromExternRef(references[3 - index]));
        }

        // Act
        var success = table.TryCopy(source, destination, sourceOffset, length);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(table.Count).IsEqualTo(3u);
            await Assert.That(source.Count).IsEqualTo(4u);
            for (uint index = 0; index < source.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(source.Get(index).AsExternRef(), references[index]))
                    .IsTrue();
            }
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), references[3 - index]))
                    .IsTrue();
            }
        }
    }
}
