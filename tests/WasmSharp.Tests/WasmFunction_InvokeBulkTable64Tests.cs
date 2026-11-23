using TUnit.Assertions.Enums;
using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task Core3の64ビットテーブルの範囲と末尾の個数0を充填する_指定した要素だけが同じ参照に変わる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = InstantiateTableRangeOperation(context, [0x7E, 0x6F, 0x7E], [0xFC, 0x11, 1]);
        var table = instance.GetTable("wide");
        var original = PopulateTableReferences(table);
        var value = new object();
        var fill = instance.GetFunction("run");

        // Act
        fill.Invoke(context, [WasmValue.FromI64(1), WasmValue.FromExternRef(value), WasmValue.FromI64(2)]);
        fill.Invoke(context, [WasmValue.FromI64(4), WasmValue.FromExternRef(null), WasmValue.FromI64(0)]);

        // Assert
        await AssertTableReferences(table, [original[0], value, value, original[3]]);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task アドレス幅が異なるテーブル間でコピーする_元の参照を保ち転送先の指定範囲だけを書き換える(
        bool copyToWide
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = copyToWide
            ? InstantiateTableRangeOperation(context, [0x7E, 0x7F, 0x7F], [0xFC, 0x0E, 1, 0])
            : InstantiateTableRangeOperation(context, [0x7F, 0x7E, 0x7F], [0xFC, 0x0E, 0, 1]);
        var source = instance.GetTable(copyToWide ? "narrow" : "wide");
        var destination = instance.GetTable(copyToWide ? "wide" : "narrow");
        var sourceBefore = PopulateTableReferences(source);
        var destinationBefore = PopulateTableReferences(destination);
        var destinationOffset = copyToWide ? WasmValue.FromI64(1) : WasmValue.FromI32(1);
        var sourceOffset = copyToWide ? WasmValue.FromI32(0) : WasmValue.FromI64(0);

        // Act
        instance.GetFunction("run").Invoke(context, [destinationOffset, sourceOffset, WasmValue.FromI32(2)]);

        // Assert
        using (Assert.Multiple())
        {
            await AssertTableReferences(source, sourceBefore);
            await AssertTableReferences(
                destination,
                [destinationBefore[0], sourceBefore[0], sourceBefore[1], destinationBefore[3]]
            );
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 同じ64ビットテーブルの重なる範囲にコピーする_どちらの方向でも元の順序で参照を転送できる(
        bool copyToHigherIndex
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = InstantiateTableRangeOperation(context, [0x7E, 0x7E, 0x7E], [0xFC, 0x0E, 1, 1]);
        var table = instance.GetTable("wide");
        var original = PopulateTableReferences(table);
        var copy = instance.GetFunction("run");
        var destination = copyToHigherIndex ? 1 : 0;
        var source = copyToHigherIndex ? 0 : 1;

        // Act
        copy.Invoke(context, [WasmValue.FromI64(destination), WasmValue.FromI64(source), WasmValue.FromI64(3)]);
        copy.Invoke(context, [WasmValue.FromI64(4), WasmValue.FromI64(4), WasmValue.FromI64(0)]);

        // Assert
        await AssertTableReferences(
            table,
            copyToHigherIndex
                ? [original[0], original[0], original[1], original[2]]
                : [original[1], original[2], original[3], original[3]]
        );
    }

    [Test]
    [Arguments(1L << 32, 1L)]
    [Arguments(1L, 1L << 32)]
    [Arguments(1L, -1L)]
    [Arguments(3L, 2L)]
    public async Task 高位ビットを含む位置や個数で充填範囲を超える_範囲外のtrapになり全要素の参照を保つ(
        long offset,
        long length
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = InstantiateTableRangeOperation(context, [0x7E, 0x6F, 0x7E], [0xFC, 0x11, 1]);
        var table = instance.GetTable("wide");
        var original = PopulateTableReferences(table);
        var fill = instance.GetFunction("run");
        WasmValue[] arguments = [WasmValue.FromI64(offset), WasmValue.FromExternRef(null), WasmValue.FromI64(length)];

        // Act & Assert
        var exception = await Assert.That(() => fill.Invoke(context, arguments)).ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await AssertTableReferences(table, original);
        }
    }

    [Test]
    [Arguments(1L << 32, 1L, 1L)]
    [Arguments(1L, 1L << 32, 1L)]
    [Arguments(0L, 1L, 1L << 32)]
    [Arguments(0L, -1L, 1L)]
    [Arguments(3L, 0L, 2L)]
    [Arguments(0L, 3L, 2L)]
    public async Task コピー元またはコピー先の範囲を超える_高位ビットを切り詰めず範囲外のtrapになり全要素の参照を保つ(
        long destination,
        long source,
        long length
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = InstantiateTableRangeOperation(context, [0x7E, 0x7E, 0x7E], [0xFC, 0x0E, 1, 1]);
        var table = instance.GetTable("wide");
        var original = PopulateTableReferences(table);
        var copy = instance.GetFunction("run");
        WasmValue[] arguments = [WasmValue.FromI64(destination), WasmValue.FromI64(source), WasmValue.FromI64(length)];

        // Act & Assert
        var exception = await Assert.That(() => copy.Invoke(context, arguments)).ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await AssertTableReferences(table, original);
        }
    }

    private static WasmInstance InstantiateTableRangeOperation(
        WasmExecutionContext context,
        byte[] parameterTypes,
        byte[] instruction
    )
    {
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameterTypes, [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(4, [2, 0x6F, 0, 4, 0x6F, 4, 4]),
            HostLinkingModuleBinary.Exports(("run", 0, 0), ("narrow", 1, 0), ("wide", 1, 1)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. instruction, 0x0B]))
        );
        return WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
    }

    private static object[] PopulateTableReferences(WasmTable table)
    {
        var references = new object[table.Count];
        for (uint index = 0; index < table.Count; index++)
        {
            references[index] = new object();
            table.Set(index, WasmValue.FromExternRef(references[index]));
        }
        return references;
    }

    private static async Task AssertTableReferences(WasmTable table, object?[] expected)
    {
        var actual = Enumerable.Range(0, (int)table.Count).Select(x => table.Get((uint)x).AsExternRef());
        await Assert.That(actual).IsEquivalentTo(expected, ReferenceEqualityComparer.Instance, CollectionOrdering.Matching);
    }
}
