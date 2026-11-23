using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 両幅のテーブルで参照またはnullを読み書きする_指定した要素の参照をホストと共有し他の要素を保つ(
        WasmValueKind kind,
        bool isNull
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateTableAccessInstance(context, kind);
        var wide = instance.GetTable("wide");
        var narrow = instance.GetTable("narrow");
        var neighbor = TableHandlerFixture.CreateReference(kind);
        var original = TableHandlerFixture.CreateReference(kind);
        var narrowValue = TableHandlerFixture.CreateReference(kind);
        var replacement = TableHandlerFixture.CreateReference(kind, isNull);
        var narrowIndex = WasmValue.FromI32(1);
        var wideIndex = WasmValue.FromI64(1);
        narrow.Set(0, original);
        wide.Set(0, neighbor);
        wide.Set(1, original);

        // Act
        instance.GetFunction("set32").Invoke(context, [narrowIndex, narrowValue]);
        var before = instance.GetFunction("get64").Invoke(context, [wideIndex]).Values[0];
        instance.GetFunction("set64").Invoke(context, [wideIndex, replacement]);
        var after = instance.GetFunction("get64").Invoke(context, [wideIndex]).Values[0];
        var other = instance.GetFunction("get32").Invoke(context, [narrowIndex]).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before).IsEqualTo(original);
            await Assert.That(after).IsEqualTo(replacement);
            await Assert.That(wide.Get(0)).IsEqualTo(neighbor);
            await Assert.That(wide.Get(1)).IsEqualTo(replacement);
            await Assert.That(other).IsEqualTo(narrowValue);
            await Assert.That(narrow.Get(0)).IsEqualTo(original);
            await Assert.That(narrow.Get(1)).IsEqualTo(narrowValue);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 小さな64ビットテーブルの添字に2の32乗を指定する_範囲外のtrapが発生しすべての要素を保つ(bool write)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateTableAccessInstance(context, WasmValueKind.ExternRef);
        var wide = instance.GetTable("wide");
        var first = WasmValue.FromExternRef(new object());
        var second = WasmValue.FromExternRef(new object());
        wide.Set(0, first);
        wide.Set(1, second);
        var index = WasmValue.FromI64(1L << 32);
        var function = instance.GetFunction(write ? "set64" : "get64");
        WasmValue[] arguments = write ? [index, WasmValue.FromExternRef(null)] : [index];

        // Act & Assert
        var exception = await Assert.That(() => function.Invoke(context, arguments)).ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert.That(exception.Message).StartsWith("out of bounds table access");
            await Assert.That(wide.Count).IsEqualTo(2u);
            await Assert.That(wide.Get(0)).IsEqualTo(first);
            await Assert.That(wide.Get(1)).IsEqualTo(second);
        }
    }

    private static WasmInstance CreateTableAccessInstance(WasmExecutionContext context, WasmValueKind elementType)
    {
        var kind = elementType == WasmValueKind.FuncRef ? (byte)0x70 : (byte)0x6F;
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], [kind]), ([0x7F, kind], []), ([0x7E], [kind]), ([0x7E, kind], [])),
            HostLinkingModuleBinary.Functions(0, 1, 2, 3),
            HostLinkingModuleBinary.Section(4, [2, kind, 0, 2, kind, 4, 2]),
            HostLinkingModuleBinary.Exports(
                ("get32", 0, 0),
                ("set32", 0, 1),
                ("get64", 0, 2),
                ("set64", 0, 3),
                ("narrow", 1, 0),
                ("wide", 1, 1)
            ),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x25, 0, 0x0B]),
                ([], [0x20, 0, 0x20, 1, 0x26, 0, 0x0B]),
                ([], [0x20, 0, 0x25, 1, 0x0B]),
                ([], [0x20, 0, 0x20, 1, 0x26, 1, 0x0B])
            )
        );
        return WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
    }
}
