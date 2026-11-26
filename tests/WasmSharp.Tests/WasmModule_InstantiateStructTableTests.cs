using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 構造体でテーブルを初期化する_各要素が同じ参照を持ち別インスタンスでは新たに生成する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x5F, 0]),
            // (table 2 (ref 0) (struct.new_default 0))
            HostLinkingModuleBinary.Section(4, [1, 0x40, 0, 0x64, 0, 0, 2, 0xFB, 1, 0, 0x0B]),
            HostLinkingModuleBinary.Exports(("items", 1, 0))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();
        var context = new WasmExecutionContext();

        // Act
        var first = module.Instantiate(context, []).GetTable("items");
        var second = module.Instantiate(context, []).GetTable("items");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first[0].Kind).IsEqualTo(WasmValueKind.StructRef);
            await Assert.That(first[1]).IsEqualTo(first[0]);
            await Assert.That(second[0]).IsNotEqualTo(first[0]);
        }
    }

    [Test]
    public async Task 要素式で構造体を生成しglobalも参照する_各生成式は別の実体を持ちglobalの参照は共有する()
    {
        // Arrange
        byte[] create = [0xFB, 0, 0, 0x0B]; // (struct.new 0)
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x5F, 0]),
            HostLinkingModuleBinary.Tables((0x6E, 3, null)),
            HostLinkingModuleBinary.Section(6, [1, 0x64, 0, 0, .. create]),
            HostLinkingModuleBinary.Exports(("items", 1, 0), ("shared", 3, 0)),
            // (elem (table 0) (i32.const 0) (ref 0) (global.get 0) (struct.new 0) (struct.new 0))
            HostLinkingModuleBinary.Section(9, [1, 6, 0, 0x41, 0, 0x0B, 0x64, 0, 3, 0x23, 0, 0x0B, .. create, .. create])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();

        // Act
        var instance = module.Instantiate(new WasmExecutionContext(), []);
        var shared = instance.GetGlobal("shared");
        var table = instance.GetTable("items");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shared.Kind).IsEqualTo(WasmValueKind.StructRef);
            await Assert.That(table[0]).IsEqualTo(shared);
            await Assert.That(new HashSet<WasmValue> { shared, table[1], table[2] }).Count().IsEqualTo(3);
        }
    }
}
