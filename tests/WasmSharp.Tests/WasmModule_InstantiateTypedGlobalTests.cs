using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 具体的な関数参照のglobalをnullableとしてimportする_不変なら同じ実体を共有し可変なら拒否する(
        bool isMutable
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var mutability = (byte)(isMutable ? 1 : 0);
        var providerBytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(6, [1, 0x64, 0, mutability, 0xD2, 0, 0x0B]),
            HostLinkingModuleBinary.Exports(("target", 0, 0), ("g", 3, 0)),
            HostLinkingModuleBinary.Code(([], [0x0B]))
        );
        var provider = WasmModule.Decode(providerBytes).Validate().Instantiate(context, []);
        var host = new WasmHostModule("env");
        host.Define("g", provider.GetGlobalResource("g"));
        var consumerBytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], []), ([], [])),
            HostLinkingModuleBinary.Imports(("env", "g", 3, [0x63, 1, mutability])),
            HostLinkingModuleBinary.Exports(("reg", 3, 0))
        );
        var consumer = WasmModule.Decode(consumerBytes).Validate();

        if (isMutable)
        {
            // Act & Assert
            var failure = await Assert
                .That(() => consumer.Instantiate(context, [host]))
                .ThrowsExactly<WasmInstantiateException>();
            await Assert.That(failure!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            return;
        }

        // Act
        var imported = consumer.Instantiate(context, [host]).GetGlobalResource("reg");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(imported).IsSameReferenceAs(provider.GetGlobalResource("g"));
            await Assert.That(imported.Value.AsFuncRef()).IsSameReferenceAs(provider.GetFunction("target"));
        }
    }
}
