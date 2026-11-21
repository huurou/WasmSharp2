using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 最大値が64ビット全域のテーブルを定義する_最小要素数で割り当てて別名でも同じ参照を取得できる()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(4, Convert.FromHexString("016F0501FFFFFFFFFFFFFFFFFF01")),
            HostLinkingModuleBinary.Exports(("table", 1, 0), ("alias", 1, 0))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();
        var reference = new object();

        // Act
        var instance = module.Instantiate(new WasmExecutionContext(), []);
        var table = instance.GetTable("table");
        var alias = instance.GetTable("alias");
        var initial = table.Get(0);
        alias.Set(0, WasmValue.FromExternRef(reference));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(alias).IsSameReferenceAs(table);
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(table.Type.MaximumElements).IsEqualTo(ulong.MaxValue);
            await Assert.That(initial.AsExternRef()).IsNull();
            await Assert.That(table[0UL].AsExternRef()).IsSameReferenceAs(reference);
        }
    }

    [Test]
    public async Task 両幅のテーブルを定義とimportに含める_増大済みのテーブルをリンクして再export先と共有できる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var host = new WasmHostModule("env");
        var small = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(1, 3));
        var wide = WasmTable.Create(new WasmTableType(WasmValueKind.ExternRef, WasmValueKind.I64, 0, ulong.MaxValue));
        wide.TryGrow(2, WasmValue.FromExternRef(null), out _);
        host.Define("small", small);
        host.Define("wide", wide);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(
                ("env", "small", 1, [0x70, 1, 1, 3]),
                ("env", "wide", 1, Convert.FromHexString("6F0502FFFFFFFFFFFFFFFFFF01"))
            ),
            HostLinkingModuleBinary.Section(4, [2, 0x70, 1, 1, 3, 0x6F, 4, 1]),
            HostLinkingModuleBinary.Exports(("small", 1, 0), ("wide", 1, 1), ("definedSmall", 1, 2), ("definedWide", 1, 3))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();
        var reference = new object();

        // Act
        var first = module.Instantiate(context, [host]);
        var reexports = new WasmHostModule("env");
        reexports.Define("small", first.GetTable("definedSmall"));
        reexports.Define("wide", first.GetTable("wide"));
        var second = module.Instantiate(context, [reexports]);
        second.GetTable("wide").Set(1, WasmValue.FromExternRef(reference));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetTable("small")).IsSameReferenceAs(small);
            await Assert.That(second.GetTable("small")).IsSameReferenceAs(first.GetTable("definedSmall"));
            await Assert.That(first.GetTable("definedWide").Type.AddressType).IsEqualTo(WasmValueKind.I64);
            await Assert.That(second.GetTable("wide")).IsSameReferenceAs(wide);
            await Assert.That(wide.Get(1).AsExternRef()).IsSameReferenceAs(reference);
        }
    }

    [Test]
    [Arguments("700001", WasmValueKind.I64, WasmValueKind.FuncRef, 2UL)]
    [Arguments("700401", WasmValueKind.I32, WasmValueKind.FuncRef, 2UL)]
    [Arguments("700401", WasmValueKind.I64, WasmValueKind.ExternRef, 2UL)]
    [Arguments("70048080808010", WasmValueKind.I64, WasmValueKind.FuncRef, ulong.MaxValue)]
    [Arguments("7005018080808010", WasmValueKind.I64, WasmValueKind.FuncRef, null)]
    [Arguments("7005018080808010", WasmValueKind.I64, WasmValueKind.FuncRef, (1UL << 32) + 1)]
    public async Task テーブルのアドレス型か要素型か上下限がimport宣言と異なる_型不一致でリンクが不成立になる(
        string declaration,
        WasmValueKind addressType,
        WasmValueKind elementType,
        ulong? maximum
    )
    {
        // Arrange
        var host = new WasmHostModule("env");
        host.Define("t", WasmTable.Create(new WasmTableType(elementType, addressType, 1, maximum)));
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "t", 1, Convert.FromHexString(declaration)))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(new WasmExecutionContext(), [host]))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            await Assert.That(exception.Message).StartsWith("incompatible import type");
        }
    }

    [Test]
    public async Task 有効な64ビットテーブルの初期要素数が保持上限を超える_インスタンス化時に定義の位置を返す()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(4, Convert.FromHexString("0170048080808010"))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(new WasmExecutionContext(), []))
            .ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Instantiate, 11, null, 4));
        }
    }
}
