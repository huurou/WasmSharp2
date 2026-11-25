using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 整数演算で生成したi31でglobalとテーブルを初期化する_importした値を使った結果を取得できる()
    {
        // Arrange
        var host = new WasmHostModule("env");
        host.Define("base", new WasmGlobal(new WasmGlobalType(WasmValueKind.I32, false), WasmValue.FromI32(40)));
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Imports(("env", "base", 3, [0x7F, 0])),
            HostLinkingModuleBinary.Functions(0),
            // (table 2 (ref i31) (ref.i31 (i32.sub (global.get 0) (i32.const 5))))
            HostLinkingModuleBinary.Section(4, [1, 0x40, 0, 0x64, 0x6C, 0, 2, 0x23, 0, 0x41, 5, 0x6B, 0xFB, 0x1C, 0x0B]),
            // (global (ref i31) (ref.i31 (i32.add (global.get 0) (i32.const 2))))
            HostLinkingModuleBinary.Section(6, [1, 0x64, 0x6C, 0, 0x23, 0, 0x41, 2, 0x6A, 0xFB, 0x1C, 0x0B]),
            HostLinkingModuleBinary.Exports(("table", 1, 0), ("read", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x23, 1, 0xFB, 0x1E, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, [host]);
        var fromGlobal = instance.GetFunction("read").Invoke(context, []).Values[0].AsI32();
        var table = instance.GetTable("table");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fromGlobal).IsEqualTo(42);
            await Assert.That(table[0].AsI31Unsigned()).IsEqualTo(35);
            await Assert.That(table[1].AsI31Unsigned()).IsEqualTo(35);
        }
    }

    [Test]
    public async Task 不変globalから作ったi31をアクティブ要素とパッシブ要素に使う_各要素をanyrefテーブルへ配置できる()
    {
        // Arrange
        // (elem (table 0) (i32.const 0) i31ref (ref.i31 (global.get 0)))
        byte[] active = [6, 0, 0x41, 0, 0x0B, 0x6C, 1, 0x23, 0, 0xFB, 0x1C, 0x0B];
        // (elem i31ref (ref.i31 (i32.mul (global.get 0) (i32.const 2))))
        byte[] passive = [5, 0x6C, 1, 0x23, 0, 0x41, 2, 0x6C, 0xFB, 0x1C, 0x0B];
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Tables((0x6E, 2, null)),
            HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 21, 0x0B])),
            HostLinkingModuleBinary.Exports(("table", 1, 0), ("init", 0, 0)),
            HostLinkingModuleBinary.Section(9, [2, .. active, .. passive]),
            HostLinkingModuleBinary.Code(([], [0x41, 1, 0x41, 0, 0x41, 1, 0xFC, 0x0C, 1, 0, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        instance.GetFunction("init").Invoke(context, []);
        var table = instance.GetTable("table");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(table[0].AsI31Unsigned()).IsEqualTo(21);
            await Assert.That(table[1].AsI31Unsigned()).IsEqualTo(42);
        }
    }
}
