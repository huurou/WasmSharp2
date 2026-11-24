using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 非nullの型付きテーブルをsetとgrowとfillで更新する_拡張前の要素数と各位置の関数実体を保つ()
    {
        // Arrange
        byte[] set = [0x41, 1, 0xD2, 1, 0x26, 0]; // 要素1をreplacementに変更する。
        byte[] grow = [0xD2, 1, 0x41, 3, 0xFC, 0x0F, 0]; // replacementを3個追加し、拡張前の要素数をスタックに残す。
        byte[] fill = [0x41, 2, 0xD2, 0, 0x41, 2, 0xFC, 0x11, 0]; // 要素2と3をoriginalで埋める。
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Functions(0, 0, 1),
            HostLinkingModuleBinary.Section(4, [1, 0x40, 0, 0x64, 0, 1, 2, 5, 0xD2, 0, 0x0B]),
            HostLinkingModuleBinary.Exports(("original", 0, 0), ("replacement", 0, 1), ("update", 0, 2), ("table", 1, 0)),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [0x0B]), ([], [.. set, .. grow, .. fill, 0x0B]))
        );
        var context = new WasmExecutionContext();
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var table = instance.GetTable("table");
        var original = instance.GetFunction("original");
        var replacement = instance.GetFunction("replacement");

        // Act
        var previousSize = instance.GetFunction("update").Invoke(context, []).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(previousSize).IsEqualTo(2);
            await Assert.That(table.Count).IsEqualTo(5u);
            await Assert.That(table.Get(0).AsFuncRef()).IsSameReferenceAs(original);
            await Assert.That(table.Get(1).AsFuncRef()).IsSameReferenceAs(replacement);
            await Assert.That(table.Get(2).AsFuncRef()).IsSameReferenceAs(original);
            await Assert.That(table.Get(3).AsFuncRef()).IsSameReferenceAs(original);
            await Assert.That(table.Get(4).AsFuncRef()).IsSameReferenceAs(replacement);
        }
    }
}
