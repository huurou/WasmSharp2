using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("1000")] // call $target
    [Arguments("4100110300")] // i32.const 0, call_indirect (type 3)
    [Arguments("D2001403")] // ref.func $target, call_ref 3
    [Arguments("D2011402")] // ref.func $base, call_ref $sub
    public async Task 上位型の関数参照を下位型の引数または呼び出し先として使う_検証時に型不一致を報告する(string call)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Functions(3, 1), // $targetの引数は(ref $sub)で、関数1自身の型は$base
            HostLinkingModuleBinary.Tables((0x70, 1, null)),
            HostLinkingModuleBinary.Exports(("target", 0, 0), ("base", 0, 1)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]), ([], [0xD2, 1, .. Convert.FromHexString(call), 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var failure = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.Message).StartsWith("type mismatch");
            await Assert.That(failure.Location?.FunctionIndex).IsEqualTo(1u);
        }
    }
}
