using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 下位型の関数参照を上位型のcall_refで呼ぶ_引数に渡した別の関数参照を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var callee = RecursiveFunctionCalls.CreateProvider(context).GetFunction("sub");
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Functions(2, 3),
            HostLinkingModuleBinary.Exports(("value", 0, 0), ("run", 0, 1)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x0B]),
                ([], [0xD2, 0, 0x20, 0, 0x14, 1, 0x0B]) // ref.func $value, local.get 0, call_ref $base
            )
        );
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);

        // Act
        var result = instance.GetFunction("run").Invoke(context, [WasmValue.FromFuncRef(callee)]);

        // Assert
        await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(instance.GetFunction("value"));
    }

    [Test]
    public async Task 上位型を指定して別モジュールの下位型の関数を間接呼び出しする_引数に渡した関数参照を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var subFunction = RecursiveFunctionCalls.CreateProvider(context).GetFunction("sub");
        var caller = CreateRecursiveIndirectCaller(context, subFunction, expectedTypeIndex: 1);

        // Act
        var result = caller.Invoke(context, [WasmValue.FromFuncRef(subFunction)]);

        // Assert
        await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(subFunction);
    }

    [Test]
    public async Task 下位型を指定して別モジュールの上位型の関数を間接呼び出しする_型不一致のtrapになる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var source = RecursiveFunctionCalls.CreateProvider(context);
        var caller = CreateRecursiveIndirectCaller(context, source.GetFunction("base"), expectedTypeIndex: 2);
        var argument = WasmValue.FromFuncRef(source.GetFunction("sub"));

        // Act & Assert
        var failure = await Assert.That(() => caller.Invoke(context, [argument])).ThrowsExactly<WasmTrapException>();
        await Assert.That(failure?.Reason).IsEqualTo(WasmTrapReason.IndirectCallTypeMismatch);
    }

    private static WasmFunction CreateRecursiveIndirectCaller(
        WasmExecutionContext context,
        WasmFunction target,
        byte expectedTypeIndex
    )
    {
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Functions(3),
            HostLinkingModuleBinary.Tables((0x70, 1, null)),
            HostLinkingModuleBinary.Exports(("run", 0, 0), ("table", 1, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x41, 0, 0x11, expectedTypeIndex, 0, 0x0B]))
        );
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        instance.GetTable("table").Set(0, WasmValue.FromFuncRef(target));
        return instance.GetFunction("run");
    }
}
