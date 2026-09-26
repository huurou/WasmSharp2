using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostStartから別instanceを呼ぶ_所有instanceの上限で中断した後に独立Instantiateできる(
        bool withInstance
    )
    {
        // Arrange
        var target = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 1, 0x0B]), ([], [0x0B]))
                )
            )
            .Validate()
            .Instantiate([], new(2))
            .GetFunction("run");
        var calls = 0;
        WasmResults Callback(ReadOnlySpan<WasmValue> arguments)
        {
            calls++;
            return target.Invoke([]);
        }
        var host = new WasmHostModule("env");
        host.Define(
            "start",
            withInstance
                ? WasmFunction.CreateHost(new([], []), (_, arguments) => Callback(arguments))
                : WasmFunction.CreateHost(new([], []), Callback)
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
                    HostLinkingModuleBinary.Start(0)
                )
            )
            .Validate();
        WasmInstance? returned = null;

        // Act & Assert
        var exception = await Assert
            .That(() => returned = module.Instantiate([host], new(2)))
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(returned).IsNull();
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(2);
            // callback内の公開Invokeで発生した例外は、元の段階を維持する。
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
        }

        // Act
        var instance = module.Instantiate([host], new(3));
        var result = target.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance).IsNotNull();
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(result.Values).IsEmpty();
        }
    }
}
