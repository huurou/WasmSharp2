using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 分岐なし関数を別instanceから反復実行する_結果と共有する静的コードを保持する()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F, 0x6F], [0x7F, 0x6F])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F), (1, 0x6F)],
                            [0x20, 0, 0x21, 2, 0x20, 1, 0x21, 3, 0x20, 2, 0x20, 3, 0x0B]
                        )
                    )
                )
            )
            .Validate();
        var firstFunction = (DefinedFunction)module.Instantiate([]).GetFunction("run");
        var secondFunction = (DefinedFunction)module.Instantiate([]).GetFunction("run");
        var code = module.FunctionCodes[0];
        var instructions = code.Instructions.ToArray();
        var branchTargets = code.BranchTargets.ToArray();
        var locals = code.Locals.ToArray();
        var firstReference = new object();
        var secondReference = new object();

        // Act
        var first = firstFunction.Invoke([
            WasmValue.FromI32(42),
            WasmValue.FromExternRef(firstReference),
        ]);
        var second = secondFunction.Invoke([
            WasmValue.FromI32(-7),
            WasmValue.FromExternRef(secondReference),
        ]);
        var repeated = firstFunction.Invoke([WasmValue.FromI32(73), WasmValue.FromExternRef(null)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(first.Values[1].AsExternRef()).IsSameReferenceAs(firstReference);
            await Assert.That(second.Values[0].AsI32()).IsEqualTo(-7);
            await Assert.That(second.Values[1].AsExternRef()).IsSameReferenceAs(secondReference);
            await Assert.That(repeated.Values[0].AsI32()).IsEqualTo(73);
            await Assert.That(repeated.Values[1].AsExternRef()).IsNull();
            await Assert.That(firstFunction.Code).IsSameReferenceAs(code);
            await Assert.That(secondFunction.Code).IsSameReferenceAs(code);
            await Assert.That(code.Instructions.SequenceEqual(instructions)).IsTrue();
            await Assert.That(code.BranchTargets.SequenceEqual(branchTargets)).IsTrue();
            await Assert.That(code.Locals.SequenceEqual(locals)).IsTrue();
            await Assert.That(code.LocalCount).IsEqualTo(2UL);
            await Assert.That(code.MaxOperandStack).IsEqualTo(2);
        }
    }
}
