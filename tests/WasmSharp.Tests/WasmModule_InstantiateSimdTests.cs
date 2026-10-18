using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task V128の不変と可変globalをimportする_共有getとsetとexportの同一性を保ち定義をinstanceごとに分ける()
    {
        // Arrange
        var initial = WasmValue.FromV128(0x7FF8123456789ABC, 0x8000000000000000);
        var updated = WasmValue.FromV128(0xFEDCBA9876543210, 0x0123456789ABCDEF);
        var seed = new WasmGlobal(new(WasmValueKind.V128, false), initial);
        var shared = new WasmGlobal(new(WasmValueKind.V128, true), initial);
        var host = new WasmHostModule("env");
        host.Define("seed", seed);
        host.Define("shared", shared);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7B]), ([0x7B], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "seed", 3, [0x7B, 0]),
                        ("env", "shared", 3, [0x7B, 1])
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Globals((0x7B, true, [0x23, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("seed", 3, 0),
                        ("shared", 3, 1),
                        ("defined", 3, 2),
                        ("read", 0, 0),
                        ("write", 0, 1)
                    ),
                    HostLinkingModuleBinary.Code(
                        ([], [0x23, 1, 0x0B]),
                        ([], [0x20, 0, 0x24, 1, 0x0B])
                    )
                )
            )
            .Validate();

        // Act
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);
        var before = first.GetFunction("read").Invoke([]).Values[0];
        var written = first.GetFunction("write").Invoke([updated]);
        var fromHost = shared.Value;
        var fromSecond = second.GetFunction("read").Invoke([]).Values[0];
        shared.Value = initial;
        var afterHost = first.GetFunction("read").Invoke([]).Values[0];
        first.GetGlobalResource("defined").Value = updated;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobalResource("seed")).IsSameReferenceAs(seed);
            await Assert.That(second.GetGlobalResource("seed")).IsSameReferenceAs(seed);
            await Assert.That(first.GetGlobalResource("shared")).IsSameReferenceAs(shared);
            await Assert.That(second.GetGlobalResource("shared")).IsSameReferenceAs(shared);
            await Assert.That(first.GetGlobalResource("seed").Type.IsMutable).IsFalse();
            await Assert.That(before).IsEqualTo(initial);
            await Assert.That(written.Values).IsEmpty();
            await Assert.That(fromHost).IsEqualTo(updated);
            await Assert.That(fromSecond).IsEqualTo(updated);
            await Assert.That(afterHost).IsEqualTo(initial);
            await Assert.That(first.GetGlobal("seed")).IsEqualTo(initial);
            await Assert.That(first.GetGlobal("defined")).IsEqualTo(updated);
            await Assert.That(second.GetGlobal("defined")).IsEqualTo(initial);
            await Assert
                .That(first.GetGlobalResource("defined"))
                .IsNotSameReferenceAs(second.GetGlobalResource("defined"));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task V128定数でglobalを初期化する_両入力の4段階で初期値をそのまま取得する(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Globals(
                (0x7B, true, Convert.FromHexString("FD0CBC9A78563412F87F00000000000000800B"))
            ),
            HostLinkingModuleBinary.Exports(("value", 3, 0), ("read", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x23, 0, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        Array.Clear(bytes);

        // Act
        var instance = module.Validate().Instantiate([]);
        var global = instance.GetGlobalResource("value");
        var result = instance.GetFunction("read").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(global.Type).IsEqualTo(new WasmGlobalType(WasmValueKind.V128, true));
            await Assert
                .That(global.Value.AsV128())
                .IsEqualTo((0x7FF8123456789ABCUL, 0x8000000000000000UL));
            await Assert.That(result.Values[0].AsV128()).IsEqualTo(global.Value.AsV128());
        }
    }
}
