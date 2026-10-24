using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 同じ深さの直接call回数を増やす_割当が回数に比例せず結果を保つ()
    {
        // Arrange
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], [0x7F])),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                // 引数の回数だけ同じ関数を深さ2で呼び、各結果を破棄する
                                .. Convert.FromHexString("034010011A200041016B22000D000B412A0B"),
                            ]
                        ),
                        ([], [0x41, 0x2A, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([], new(2))
            .GetFunction("run");
        WasmValue[] fewCalls = [WasmValue.FromI32(1)];
        WasmValue[] manyCalls = [WasmValue.FromI32(1024)];
        for (var index = 0; index < 16; index++)
        {
            function.Invoke(fewCalls);
            function.Invoke(manyCalls);
        }

        // Act
        var beforeFew = GC.GetAllocatedBytesForCurrentThread();
        var fewResult = function.Invoke(fewCalls);
        var fewAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeFew;
        var beforeMany = GC.GetAllocatedBytesForCurrentThread();
        var manyResult = function.Invoke(manyCalls);
        var manyAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeMany;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fewResult.Values.Single().AsI32()).IsEqualTo(42);
            await Assert.That(manyResult.Values.Single().AsI32()).IsEqualTo(42);
            // 最大深さと戻り値が同じなら、反復増加による追加割当を4KiB以内に抑える
            await Assert.That(manyAllocated - fewAllocated).IsLessThanOrEqualTo(4096L);
        }
    }
}
