using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments((byte)0)]
    [Arguments((byte)1)]
    [Arguments((byte)2)]
    [Arguments((byte)3)]
    [Arguments((byte)4)]
    [Arguments((byte)5)]
    [Arguments((byte)6)]
    [Arguments((byte)7)]
    public async Task 全要素形式から初期化する_modeに従って保持または破棄し反復dropできる(
        byte flags
    )
    {
        // Arrange
        var (module, host, firstValue, secondValue) = CreateBulkTableModule(0x70, flags);
        var instance = module.Instantiate([host]);
        var expected =
            flags < 4
                ? new[]
                {
                    WasmValue.FromFuncRef(instance.GetFunction("init")),
                    WasmValue.FromFuncRef(instance.GetFunction("copy")),
                }
                : new[] { firstValue, secondValue };
        var table = instance.GetTable("a");
        var passive = flags is 1 or 5;
        var active = flags is 0 or 2 or 4 or 6;

        // Act
        if (passive)
        {
            instance
                .GetFunction("init")
                .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(2)]);
            instance
                .GetFunction("init")
                .Invoke([WasmValue.FromI32(2), WasmValue.FromI32(0), WasmValue.FromI32(2)]);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(table.Get(0))
                .IsEqualTo(active || passive ? expected[0] : WasmValue.FromFuncRef(null));
            await Assert
                .That(table.Get(1))
                .IsEqualTo(active || passive ? expected[1] : WasmValue.FromFuncRef(null));
            if (passive)
            {
                await Assert.That(table.Get(2)).IsEqualTo(expected[0]);
                await Assert.That(table.Get(3)).IsEqualTo(expected[1]);
            }
        }

        // Assert
        if (!passive)
        {
            instance
                .GetFunction("init")
                .Invoke([WasmValue.FromI32(4), WasmValue.FromI32(0), WasmValue.FromI32(0)]);
            await Assert
                .That(() =>
                    instance
                        .GetFunction("init")
                        .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(1)])
                )
                .ThrowsExactly<WasmTrapException>();
        }

        // Act
        instance.GetFunction("drop").Invoke([]);
        instance.GetFunction("drop").Invoke([]);
        instance
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(4), WasmValue.FromI32(0), WasmValue.FromI32(0)]);
        var before = Enumerable.Range(0, 4).Select(x => table.Get((uint)x)).ToArray();

        // Assert
        foreach (var (destination, source, length) in new[] { (0, 0, 1), (0, 1, 0), (5, 0, 0) })
        {
            await Assert
                .That(() =>
                    instance
                        .GetFunction("init")
                        .Invoke([
                            WasmValue.FromI32(destination),
                            WasmValue.FromI32(source),
                            WasmValue.FromI32(length),
                        ])
                )
                .ThrowsExactly<WasmTrapException>();
            await Assert
                .That(Enumerable.Range(0, 4).Select(x => table.Get((uint)x)).SequenceEqual(before))
                .IsTrue();
        }
    }

    [Test]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task passive要素を別instanceから初期化する_dropは転送済み参照と他instanceに影響しない(
        byte kind
    )
    {
        // Arrange
        var (module, host, firstValue, secondValue) = CreateBulkTableModule(kind, 5);
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);

        // Act
        first
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(2)]);
        first.GetFunction("drop").Invoke([]);
        second
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(2), WasmValue.FromI32(0), WasmValue.FromI32(2)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetTable("a")).IsSameReferenceAs(second.GetTable("a"));
            await Assert.That(first.GetTable("a").Get(0)).IsEqualTo(firstValue);
            await Assert.That(first.GetTable("a").Get(1)).IsEqualTo(secondValue);
            await Assert.That(first.GetTable("a").Get(2)).IsEqualTo(firstValue);
            await Assert.That(first.GetTable("a").Get(3)).IsEqualTo(secondValue);
            await Assert
                .That(() =>
                    first
                        .GetFunction("init")
                        .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(1)])
                )
                .ThrowsExactly<WasmTrapException>();
        }
    }

    [Test]
    [Arguments((byte)0x70, false)]
    [Arguments((byte)0x6F, false)]
    [Arguments((byte)0x70, true)]
    [Arguments((byte)0x6F, true)]
    public async Task 同型table間をcopyとfillする_異なる添字と共有実体の前後重複でも参照の同一性を保つ(
        byte kind,
        bool alias
    )
    {
        // Arrange
        var (module, host, firstValue, secondValue) = CreateBulkTableModule(kind, 5, alias);
        var instance = module.Instantiate([host]);
        var source = instance.GetTable("a");
        var destination = instance.GetTable("b");
        source.Set(0, firstValue);
        source.Set(1, secondValue);
        source.Set(2, firstValue);
        source.Set(3, secondValue);

        // Act
        instance
            .GetFunction("copy")
            .Invoke([WasmValue.FromI32(1), WasmValue.FromI32(0), WasmValue.FromI32(3)]);

        // Assert
        await Assert
            .That(
                Enumerable
                    .Range(1, 3)
                    .Select(x => destination.Get((uint)x))
                    .SequenceEqual([firstValue, secondValue, firstValue])
            )
            .IsTrue();

        // Act
        instance
            .GetFunction("copy")
            .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(1), WasmValue.FromI32(3)]);

        // Assert
        await Assert
            .That(
                Enumerable
                    .Range(0, 3)
                    .Select(x => destination.Get((uint)x))
                    .SequenceEqual(
                        alias
                            ? [firstValue, secondValue, firstValue]
                            : [secondValue, firstValue, secondValue]
                    )
            )
            .IsTrue();

        // Act
        instance
            .GetFunction("fill")
            .Invoke([WasmValue.FromI32(1), secondValue, WasmValue.FromI32(3)]);

        // Assert
        await Assert
            .That(
                Enumerable
                    .Range(1, 3)
                    .Select(x => destination.Get((uint)x))
                    .All(x => x.Equals(secondValue))
            )
            .IsTrue();

        // Act
        var nullValue = kind == 0x70 ? WasmValue.FromFuncRef(null) : WasmValue.FromExternRef(null);
        instance
            .GetFunction("fill")
            .Invoke([WasmValue.FromI32(0), nullValue, WasmValue.FromI32(4)]);

        // Assert
        await Assert
            .That(
                Enumerable
                    .Range(0, 4)
                    .Select(x => destination.Get((uint)x))
                    .All(x => x.Equals(nullValue))
            )
            .IsTrue();
    }

    [Test]
    [Arguments("init", 3, 0, 2)]
    [Arguments("init", 0, 1, 2)]
    [Arguments("init", -1, 0, 0)]
    [Arguments("init", 0, int.MinValue, 0)]
    [Arguments("init", 0, 0, -1)]
    [Arguments("init", 4, 2, 0)]
    [Arguments("init", 4, 3, 0)]
    [Arguments("copy", 3, 0, 2)]
    [Arguments("copy", 0, 3, 2)]
    [Arguments("copy", -1, 0, 0)]
    [Arguments("copy", 0, -1, 0)]
    [Arguments("copy", 0, 0, int.MinValue)]
    [Arguments("copy", 4, 4, 0)]
    [Arguments("copy", 5, 4, 0)]
    [Arguments("copy", 4, 5, 0)]
    [Arguments("fill", 3, 0, 2)]
    [Arguments("fill", -1, 0, 0)]
    [Arguments("fill", 0, 0, -1)]
    [Arguments("fill", 4, 0, 0)]
    [Arguments("fill", 5, 0, 0)]
    public async Task 一括操作の符号なし範囲を検査する_末尾の0長だけ成功し範囲外は部分更新せず位置付きtrapになる(
        string operation,
        int destination,
        int source,
        int length
    )
    {
        // Arrange
        var (module, host, firstValue, secondValue) = CreateBulkTableModule(0x6F, 5);
        var instance = module.Instantiate([host]);
        var table = instance.GetTable(operation == "init" ? "a" : "b");
        for (uint index = 0; index < 4; index++)
        {
            table.Set(index, firstValue);
        }
        var functionIndex =
            operation == "init" ? 0u
            : operation == "copy" ? 1u
            : 2u;
        var offset = module.Functions[(int)functionIndex].Instructions[3].ByteOffset;
        WasmValue[] arguments =
        [
            WasmValue.FromI32(destination),
            operation == "fill" ? secondValue : WasmValue.FromI32(source),
            WasmValue.FromI32(length),
        ];

        // Act & Assert
        if (
            destination == 4
            && source
                == (
                    operation == "init" ? 2
                    : operation == "copy" ? 4
                    : 0
                )
        )
        {
            await Assert
                .That(() => instance.GetFunction(operation).Invoke(arguments))
                .ThrowsNothing();
        }
        else
        {
            var exception = await Assert
                .That(() => instance.GetFunction(operation).Invoke(arguments))
                .ThrowsExactly<WasmTrapException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Invoke, offset, functionIndex));
                await Assert
                    .That(
                        exception.Message.StartsWith(
                            "out of bounds table access",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
            }
        }
        await Assert
            .That(
                Enumerable
                    .Range(0, 4)
                    .Select(x => table.Get((uint)x))
                    .All(x => x.Equals(firstValue))
            )
            .IsTrue();
    }

    private static (
        WasmModule Module,
        WasmHostModule Host,
        WasmValue First,
        WasmValue Second
    ) CreateBulkTableModule(byte kind, byte flags, bool alias = false)
    {
        var valueKind = kind == 0x70 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef;
        var first = TableHandlerFixture.CreateReference(valueKind);
        var second = TableHandlerFixture.CreateReference(valueKind);
        var host = new WasmHostModule("env");
        var table = new WasmTable(valueKind, new(4, 4));
        host.Define("a", table);
        host.Define("b", alias ? table : new WasmTable(valueKind, new(4, 4)));
        host.Define("x", new WasmGlobal(new(valueKind, false), first));
        host.Define("y", new WasmGlobal(new(valueKind, false), second));
        byte[] mode = flags switch
        {
            0 or 4 => [flags, 0x41, 0, 0x0B],
            2 or 6 => [flags, 0, 0x41, 0, 0x0B],
            _ => [flags],
        };
        byte[] element =
        [
            .. mode,
            .. (
                flags == 0 || flags == 4
                    ? Array.Empty<byte>()
                    : new byte[] { flags < 4 ? (byte)0 : kind }
            ),
            2,
            .. (flags < 4 ? new byte[] { 0, 1 } : new byte[] { 0x23, 0, 0x0B, 0x23, 1, 0x0B }),
        ];
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7F, 0x7F, 0x7F], []),
                        ([0x7F, kind, 0x7F], []),
                        ([], [])
                    ),
                    HostLinkingModuleBinary.Imports(
                        ("env", "a", 1, [kind, 1, 4, 4]),
                        ("env", "b", 1, [kind, 1, 4, 4]),
                        ("env", "x", 3, [kind, 0]),
                        ("env", "y", 3, [kind, 0])
                    ),
                    HostLinkingModuleBinary.Functions(0, 0, 1, 2),
                    HostLinkingModuleBinary.Exports(
                        ("init", 0, 0),
                        ("copy", 0, 1),
                        ("fill", 0, 2),
                        ("drop", 0, 3),
                        ("a", 1, 0),
                        ("b", 1, 1)
                    ),
                    HostLinkingModuleBinary.Section(9, [2, 3, 0, 0, .. element]),
                    HostLinkingModuleBinary.Code(
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0xFC, 0x0C, 1, 0, 0x0B]),
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0xFC, 0x0E, 1, 0, 0x0B]),
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0xFC, 0x11, 1, 0x0B]),
                        ([], [0xFC, 0x0D, 1, 0x0B])
                    )
                )
            )
            .Validate();
        return (module, host, first, second);
    }
}
