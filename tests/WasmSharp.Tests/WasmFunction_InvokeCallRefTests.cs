using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task Nullを許す型付き引数をcall_refで呼ぶ_Invokeでnull参照のtrapを返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 0, 0x60, 1, 0x63, 0, 0]),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Exports(("run", 0, 0u)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x14, 0, 0x0B]))
        );
        var run = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []).GetFunction("run");

        // Act & Assert
        var trap = await Assert
            .That(() => run.Invoke(context, [WasmValue.FromFuncRef(null)]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(trap!.Reason).IsEqualTo(WasmTrapReason.NullFunctionReference);
            await Assert.That(trap.Message).StartsWith("null function reference");
            await Assert.That(trap.Location?.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(trap.Location?.FunctionIndex).IsEqualTo(0u);
        }
    }

    [Test]
    public async Task 再exportした関数を引数とglobalからcall_refで呼ぶ_同一性と直前の定義関数のinstanceを保つ()
    {
        // Arrange
        var context = new WasmExecutionContext();
        List<(WasmExecutionContext Context, WasmInstance Instance)> calls = [];
        var host = WasmFunction.CreateHost(new WasmFunctionType([], []), (callbackContext, instance, _) =>
        {
            calls.Add((callbackContext, instance));
            return new WasmResults([]);
        });
        var source = CreateHostCallingInstance(context, host, maxCallDepth: 1);
        var provider = new WasmHostModule("source");
        provider.Define("host", source.GetFunction("host"));
        provider.Define("defined", source.GetFunction("run"));
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 0, 0x60, 1, 0x64, 0, 0]),
            HostLinkingModuleBinary.Imports(("source", "host", 0, [0]), ("source", "defined", 0, [0])),
            HostLinkingModuleBinary.Functions(1, 0),
            HostLinkingModuleBinary.Section(6, [1, 0x64, 0, 0, 0xD2, 1, 0x0B]), // global (ref 0) = ref.func 1
            HostLinkingModuleBinary.Exports(
                ("host", 0, 0u),
                ("defined", 0, 1u),
                ("callArgument", 0, 2u),
                ("callGlobal", 0, 3u)
            ),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x14, 0, 0x0B]), ([], [0x23, 0, 0x14, 0, 0x0B]))
        );
        var caller = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, [provider]);

        // Act
        caller.GetFunction("callArgument").Invoke(context, [WasmValue.FromFuncRef(caller.GetFunction("host"))]);
        caller.GetFunction("callGlobal").Invoke(context, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(caller.GetFunction("host")).IsSameReferenceAs(host);
            await Assert.That(caller.GetFunction("defined")).IsSameReferenceAs(source.GetFunction("run"));
            await Assert.That(calls.SequenceEqual([(context, caller), (context, source)])).IsTrue();
        }
    }

    [Test]
    public async Task Ref_funcの参照先をcall_refで呼ぶ_複数の引数と結果を宣言順に渡す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7E], [0x7E, 0x7F])),
            HostLinkingModuleBinary.Functions(0, 0),
            HostLinkingModuleBinary.Exports(("swap", 0, 0u), ("run", 0, 1u)),
            HostLinkingModuleBinary.Code(([], [0x20, 1, 0x20, 0, 0x0B]), ([], [0x20, 0, 0x20, 1, 0xD2, 0, 0x14, 0, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var result = instance.GetFunction("run").Invoke(context, [WasmValue.FromI32(17), WasmValue.FromI64(42)]);

        // Assert
        await Assert.That(result.Values.SequenceEqual([WasmValue.FromI64(42), WasmValue.FromI32(17)])).IsTrue();
    }
}
