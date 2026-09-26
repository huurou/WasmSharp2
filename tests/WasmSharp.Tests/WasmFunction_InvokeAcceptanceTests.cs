using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 7)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 7)]
    public async Task Guestから両形式のhostと値を往復する_返却元を再利用しても個数とビット列と参照を保つ(
        bool withInstance,
        int count
    )
    {
        // Arrange
        byte[] allKinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F];
        WasmValue[] allValues =
        [
            WasmValue.FromI32(-42),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFFC12345),
            WasmValue.FromF64Bits(0xFFF8123456789ABC),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([]))),
            WasmValue.FromExternRef(new object()),
        ];
        WasmValue[] allOtherValues =
        [
            WasmValue.FromI32(73),
            WasmValue.FromI64(long.MaxValue),
            WasmValue.FromF32Bits(0x7FC54321),
            WasmValue.FromF64Bits(0x7FF8FEDCBA987654),
            WasmValue.FromV128(0xFEDCBA9876543210, 0x0123456789ABCDEF),
            WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([]))),
            WasmValue.FromExternRef(new object()),
        ];
        var kinds = allKinds[..count];
        var values = allValues[..count];
        var otherValues = allOtherValues[..count];
        var buffer = new WasmValue[count];
        var calls = 0;
        WasmResults Callback(ReadOnlySpan<WasmValue> arguments)
        {
            calls++;
            arguments.CopyTo(buffer);
            return new(buffer);
        }
        var type = new WasmFunctionType(
            values.Select(x => x.Kind).ToArray(),
            values.Select(x => x.Kind).ToArray()
        );
        var host = new WasmHostModule("env");
        host.Define(
            "echo",
            withInstance
                ? WasmFunction.CreateHost(type, (_, arguments) => Callback(arguments))
                : WasmFunction.CreateHost(type, Callback)
        );
        List<byte> body = [];
        for (byte index = 0; index < count; index++)
        {
            body.AddRange([0x20, index]);
        }
        body.AddRange([0x10, 0, 0x0B]);
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types((kinds, kinds)),
                    HostLinkingModuleBinary.Imports(("env", "echo", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [.. body]))
                )
            )
            .Validate()
            .Instantiate([host])
            .GetFunction("run");

        // Act
        var first = function.Invoke(values);
        var second = function.Invoke(otherValues);
        Array.Fill(buffer, WasmValue.FromI32(99));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(first.Values.SequenceEqual(values)).IsTrue();
            await Assert.That(second.Values.SequenceEqual(otherValues)).IsTrue();
            if (count == 7)
            {
                await Assert
                    .That(first.Values[5].AsFuncRef())
                    .IsSameReferenceAs(values[5].AsFuncRef());
                await Assert
                    .That(first.Values[6].AsExternRef())
                    .IsSameReferenceAs(values[6].AsExternRef());
                await Assert
                    .That(second.Values[5].AsFuncRef())
                    .IsSameReferenceAs(otherValues[5].AsFuncRef());
                await Assert
                    .That(second.Values[6].AsExternRef())
                    .IsSameReferenceAs(otherValues[6].AsExternRef());
            }
        }
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task Hostが不正な結果を返す_後続のglobal更新を実行せず次の呼出しでは回復する(
        bool withInstance,
        int invalidResult
    )
    {
        // Arrange
        var invalid = true;
        WasmResults Callback(ReadOnlySpan<WasmValue> arguments)
        {
            return !invalid
                ? new([WasmValue.FromI32(42)])
                : invalidResult switch
                {
                    0 => null!,
                    1 => new([]),
                    _ => new([WasmValue.FromI64(42)]),
                };
        }

        var host = new WasmHostModule("env");
        var type = new WasmFunctionType([], [WasmValueKind.I32]);
        host.Define(
            "value",
            withInstance
                ? WasmFunction.CreateHost(type, (_, arguments) => Callback(arguments))
                : WasmFunction.CreateHost(type, Callback)
        );
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F]), ([], [])),
                    HostLinkingModuleBinary.Imports(("env", "value", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("g", 3, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x24, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([host], new(2));

        // Act & Assert
        await Assert
            .That(() => instance.GetFunction("run").Invoke([]))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(instance.GetGlobal("g").AsI32()).IsEqualTo(0);

        // Act
        invalid = false;
        var result = instance.GetFunction("run").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(instance.GetGlobal("g").AsI32()).IsEqualTo(42);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Importした定義関数でglobalを更新する_元の共有実体を使いreturnとtrapの後を実行しない(
        bool trap
    )
    {
        // Arrange
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var host = new WasmHostModule("env");
        host.Define("g", shared);
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F, 0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 1])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("g", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F)],
                            [
                                0x23,
                                0,
                                0x20,
                                0,
                                0x22,
                                1,
                                0x1A,
                                0x20,
                                1,
                                0x24,
                                0,
                                0x20,
                                1,
                                trap ? (byte)0x00 : (byte)0x0F,
                                0x41,
                                1,
                                0x24,
                                0,
                                0x00,
                                0x0B,
                            ]
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([host]);
        var provider = new WasmHostModule("source");
        provider.Define("run", source.GetFunction("run"));
        var destination = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F, 0x7F])),
                    HostLinkingModuleBinary.Imports(("source", "run", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 9, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("g", 3, 0)),
                    HostLinkingModuleBinary.Code(([], [0x20, 0, 0x10, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([provider]);
        var function = destination.GetFunction("run");

        if (trap)
        {
            // Act & Assert
            var exception = await Assert
                .That(() => function.Invoke([WasmValue.FromI32(42)]))
                .ThrowsExactly<WasmTrapException>();
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
        }
        else
        {
            // Act
            var first = function.Invoke([WasmValue.FromI32(42)]);
            shared.Value = WasmValue.FromI32(11);
            var second = function.Invoke([WasmValue.FromI32(42)]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert
                    .That(first.Values.SequenceEqual([WasmValue.FromI32(7), WasmValue.FromI32(42)]))
                    .IsTrue();
                await Assert
                    .That(
                        second.Values.SequenceEqual([WasmValue.FromI32(11), WasmValue.FromI32(42)])
                    )
                    .IsTrue();
            }
        }
        using (Assert.Multiple())
        {
            await Assert.That(shared.Value.AsI32()).IsEqualTo(42);
            await Assert.That(source.GetGlobal("g").AsI32()).IsEqualTo(42);
            await Assert.That(destination.GetGlobal("g").AsI32()).IsEqualTo(9);
        }
    }
}
