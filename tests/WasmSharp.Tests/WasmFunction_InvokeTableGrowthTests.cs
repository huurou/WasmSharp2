using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(3UL, 2L, 1L, 3u)]
    [Arguments(1UL, 0L, 1L, 1u)]
    [Arguments(1UL, 1L, -1L, 1u)]
    [Arguments(null, 1L << 32, -1L, 1u)]
    [Arguments(null, -1L, -1L, 1u)]
    public async Task 共有する64ビットテーブルを拡張する_成否をi64で返し既存要素を保って追加要素に指定参照を設定する(
        ulong? maximum,
        long delta,
        long expectedResult,
        uint expectedCount
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.ExternRef, WasmValueKind.I64, 1, maximum));
        var original = WasmValue.FromExternRef(new object());
        var added = WasmValue.FromExternRef(new object());
        table.Set(0, original);
        var host = new WasmHostModule("env");
        host.Define("table", table);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x6F, 0x7E], [0x7E, 0x7E])),
            HostLinkingModuleBinary.Imports(("env", "table", 1, [0x6F, 4, 1])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("grow-size", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0xFC, 0x0F, 0, 0xFC, 0x10, 0, 0x0B]))
        );
        var instance = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, [host]);

        // Act
        var values = instance.GetFunction("grow-size").Invoke(context, [added, WasmValue.FromI64(delta)]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0]).IsEqualTo(WasmValue.FromI64(expectedResult));
            await Assert.That(values[1]).IsEqualTo(WasmValue.FromI64(expectedCount));
            await Assert.That(table.Count).IsEqualTo(expectedCount);
            await Assert.That(table.Get(0)).IsEqualTo(original);
            for (uint index = 1; index < expectedCount; index++)
            {
                await Assert.That(table.Get(index)).IsEqualTo(added);
            }
        }
    }

    [Test]
    public async Task 両幅のテーブルの要素数を取得する_指定したテーブルのアドレス型でそれぞれの要素数を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F, 0x7E])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(4, [2, 0x6F, 0, 1, 0x6F, 4, 2]),
            HostLinkingModuleBinary.Exports(("sizes", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0xFC, 0x10, 0, 0xFC, 0x10, 1, 0x0B]))
        );
        var instance = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);

        // Act
        var sizes = instance.GetFunction("sizes").Invoke(context, []).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(sizes[0]).IsEqualTo(WasmValue.FromI32(1));
            await Assert.That(sizes[1]).IsEqualTo(WasmValue.FromI64(2));
        }
    }
}
