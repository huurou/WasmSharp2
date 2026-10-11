using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 制御とselectを含むstartがある_両入力のDecodeとValidateは副作用を起こさない(
        bool useStream
    )
    {
        // Arrange
        var calls = 0;
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var host = new WasmHostModule("env");
        host.Define("g", global);
        host.Define(
            "callback",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(
                ("env", "callback", 0, [0]),
                ("env", "g", 3, [0x7F, 1])
            ),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 1)),
            HostLinkingModuleBinary.Start(1),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    Convert.FromHexString("0102400340010B0B41010440100005000B4108410941001B24000B")
                )
            )
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        var afterDecode = (calls, global.Value.AsI32());
        var validated = module.Validate();
        var afterValidation = (calls, global.Value.AsI32());
        var instance = module.Instantiate([host]);
        var afterInstantiation = (calls, global.Value.AsI32());
        var result = instance.GetFunction("run").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(afterDecode).IsEqualTo((0, 7));
            await Assert.That(afterValidation).IsEqualTo((0, 7));
            await Assert.That(afterInstantiation).IsEqualTo((1, 9));
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(global.Value.AsI32()).IsEqualTo(9);
            await Assert.That(result.Values.IsEmpty).IsTrue();
        }
    }
}
