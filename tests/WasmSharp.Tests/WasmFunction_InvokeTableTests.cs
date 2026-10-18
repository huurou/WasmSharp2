using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments((byte)0x70, (byte)0, false)]
    [Arguments((byte)0x70, (byte)1, true)]
    [Arguments((byte)0x6F, (byte)0, true)]
    [Arguments((byte)0x6F, (byte)1, false)]
    public async Task importと定義tableを読み書きする_指定添字と参照型とnullと同一性を保つ(
        byte kind,
        byte tableIndex,
        bool isNull
    )
    {
        // Arrange
        var module = CreateBasicTableModule(kind, tableIndex);
        var importedKind = tableIndex == 0 ? kind : (byte)(kind == 0x70 ? 0x6F : 0x70);
        var imported = new WasmTable(
            (importedKind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef),
            new(1, 4)
        );
        var host = new WasmHostModule("env");
        host.Define("t", imported);
        var instance = module.Instantiate([host]);
        var selected = instance.GetTable(tableIndex == 0 ? "imported" : "defined");
        var other = instance.GetTable(tableIndex == 0 ? "defined" : "imported");
        var before = TableHandlerFixture.CreateReference(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef)
        );
        var value = TableHandlerFixture.CreateReference(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef),
            isNull
        );
        selected.Set(0, before);
        var untouched = other.Get(0);

        // Act
        var values = instance.GetFunction("set-get").Invoke([WasmValue.FromI32(0), value]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values.SequenceEqual([before, value, WasmValue.FromI32(1)])).IsTrue();
            await Assert.That(selected.Get(0)).IsEqualTo(value);
            await Assert.That(other.Get(0)).IsEqualTo(untouched);
            await Assert.That(instance.GetTable("imported")).IsSameReferenceAs(imported);
        }
    }

    [Test]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task 共有tableを複数instanceから更新と増大する_ホストとguestが同じ内容と要素数を参照する(
        byte kind
    )
    {
        // Arrange
        var module = CreateBasicTableModule(kind, 0);
        var table = new WasmTable(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef),
            new(1, 4)
        );
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);
        var original = TableHandlerFixture.CreateReference(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef)
        );
        var added = TableHandlerFixture.CreateReference(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef)
        );
        table.Set(0, original);

        // Act
        var firstValues = first.GetFunction("set-get").Invoke([WasmValue.FromI32(0), added]).Values;
        var grown = second.GetFunction("grow-size").Invoke([original, WasmValue.FromI32(2)]).Values;
        var zero = first.GetFunction("grow-size").Invoke([added, WasmValue.FromI32(0)]).Values;
        var secondValues = second
            .GetFunction("set-get")
            .Invoke([WasmValue.FromI32(2), added])
            .Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(firstValues.SequenceEqual([original, added, WasmValue.FromI32(1)]))
                .IsTrue();
            await Assert
                .That(grown.SequenceEqual([WasmValue.FromI32(1), WasmValue.FromI32(3)]))
                .IsTrue();
            await Assert
                .That(zero.SequenceEqual([WasmValue.FromI32(3), WasmValue.FromI32(3)]))
                .IsTrue();
            await Assert
                .That(secondValues.SequenceEqual([original, added, WasmValue.FromI32(3)]))
                .IsTrue();
            await Assert.That(table.Count).IsEqualTo(3u);
            await Assert.That(table.Get(0)).IsEqualTo(added);
            await Assert.That(table.Get(1)).IsEqualTo(original);
            await Assert.That(table.Get(2)).IsEqualTo(added);
            await Assert
                .That(ReferenceEquals(first.GetTable("defined"), second.GetTable("defined")))
                .IsFalse();
        }
    }

    [Test]
    [Arguments((byte)0x70, 4)]
    [Arguments((byte)0x6F, 4)]
    [Arguments((byte)0x70, -1)]
    [Arguments((byte)0x6F, int.MinValue)]
    public async Task 増分が宣言上限または保持上限を超える_負の1を返し要素数と内容を変更しない(
        byte kind,
        int delta
    )
    {
        // Arrange
        var module = CreateBasicTableModule(kind, 0, delta < 0 ? null : 4);
        var table = new WasmTable(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef),
            new(1, delta < 0 ? null : 4)
        );
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var instance = module.Instantiate([host]);
        var original = TableHandlerFixture.CreateReference(
            (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef)
        );
        table.Set(0, original);

        // Act
        var values = instance
            .GetFunction("grow-size")
            .Invoke([
                TableHandlerFixture.CreateReference(
                    (kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef),
                    true
                ),
                WasmValue.FromI32(delta),
            ])
            .Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(values.SequenceEqual([WasmValue.FromI32(-1), WasmValue.FromI32(1)]))
                .IsTrue();
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }

    [Test]
    [Arguments(false, -1)]
    [Arguments(false, int.MinValue)]
    [Arguments(true, 1)]
    [Arguments(true, -1)]
    public async Task 符号なし要素添字が範囲外_内容を変更せず関数と命令位置付きのInvoke_trapになる(
        bool write,
        int index
    )
    {
        // Arrange
        byte[] operation = write ? [0x20, 0, 0x20, 1, 0x26, 0] : [0x20, 0, 0x25, 0, 0x1A];
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], []), ([0x7F, 0x6F], [])),
                HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Tables((0x6F, 1, 1)),
                HostLinkingModuleBinary.Exports(("run", 0, 1), ("t", 1, 0)),
                HostLinkingModuleBinary.Code(([], [.. operation, 0x0B]))
            )
        );
        var expectedOffset = module.Functions[0].Instructions[write ? 2 : 1].ByteOffset;
        var host = new WasmHostModule("env");
        host.Define("f", WasmFunction.CreateHost(new([], []), _ => new([])));
        var instance = module.Validate().Instantiate([host]);
        var original = WasmValue.FromExternRef(new object());
        instance.GetTable("t").Set(0, original);

        // Act & Assert
        var exception = await Assert
            .That(() =>
                instance
                    .GetFunction("run")
                    .Invoke([WasmValue.FromI32(index), WasmValue.FromExternRef(null)])
            )
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "out of bounds table access",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Invoke, expectedOffset, 1));
            await Assert.That(instance.GetTable("t").Get(0)).IsEqualTo(original);
        }
    }

    [Test]
    public async Task ホスト操作が不正またはcallbackがOOMを投げる_API例外と同じホスト例外を伝播する()
    {
        // Arrange
        var failure = new OutOfMemoryException("host callback");
        var host = new WasmHostModule("env");
        host.Define("f", WasmFunction.CreateHost(new([], []), _ => throw failure));
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Tables((0x6F, 1, 2)),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("t", 1, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x10, 0, 0xD0, 0x6F, 0x41, 1, 0xFC, 0x0F, 0, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([host]);
        var table = instance.GetTable("t");

        // Act & Assert
        await Assert
            .That(() => table.Get(uint.MaxValue))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert
            .That(() => table.Set(0, WasmValue.FromI32(1)))
            .ThrowsExactly<ArgumentException>();
        await Assert
            .That(() => table.TryGrow(0, WasmValue.FromFuncRef(null), out _))
            .ThrowsExactly<ArgumentException>();
        var exception = await Assert
            .That(() => instance.GetFunction("run").Invoke([]))
            .ThrowsExactly<OutOfMemoryException>();
        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(table.Count).IsEqualTo(1u);
    }

    private static WasmModule CreateBasicTableModule(byte kind, byte tableIndex, uint? maximum = 4)
    {
        var otherKind = (byte)(kind == 0x70 ? 0x6F : 0x70);
        var importedKind = tableIndex == 0 ? kind : otherKind;
        var definedKind = tableIndex == 1 ? kind : otherKind;
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7F, kind], [kind, kind, 0x7F]),
                        ([kind, 0x7F], [0x7F, 0x7F])
                    ),
                    HostLinkingModuleBinary.Imports(
                        (
                            "env",
                            "t",
                            1,
                            [importedKind, .. HostLinkingModuleBinary.Limits(1, maximum)]
                        )
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Tables((definedKind, 1, maximum)),
                    HostLinkingModuleBinary.Exports(
                        ("set-get", 0, 0),
                        ("grow-size", 0, 1),
                        ("imported", 1, 0),
                        ("defined", 1, 1)
                    ),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                0x20,
                                0,
                                0x25,
                                tableIndex,
                                0x20,
                                0,
                                0x20,
                                1,
                                0x26,
                                tableIndex,
                                0x20,
                                0,
                                0x25,
                                tableIndex,
                                0xFC,
                                0x10,
                                tableIndex,
                                0x0B,
                            ]
                        ),
                        (
                            [],
                            [0x20, 0, 0x20, 1, 0xFC, 0x0F, tableIndex, 0xFC, 0x10, tableIndex, 0x0B]
                        )
                    )
                )
            )
            .Validate();
    }
}
