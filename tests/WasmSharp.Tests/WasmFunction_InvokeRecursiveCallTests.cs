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
    public async Task 再帰関数型を指定して別モジュールの関数を間接呼び出しする_下位型を受け入れ逆方向ではtrapになる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var source = RecursiveFunctionCalls.CreateProvider(context);
        var subFunction = source.GetFunction("sub");
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Functions(3, 3),
            HostLinkingModuleBinary.Tables((0x70, 2, null)),
            HostLinkingModuleBinary.Exports(("asBase", 0, 0), ("asSub", 0, 1), ("table", 1, 0)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x41, 0, 0x11, 1, 0, 0x0B]), // 型$baseで要素0を呼ぶ
                ([], [0x20, 0, 0x41, 1, 0x11, 2, 0, 0x0B]) // 型$subで要素1を呼ぶ
            )
        );
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var table = instance.GetTable("table");
        table.Set(0, WasmValue.FromFuncRef(subFunction));
        table.Set(1, WasmValue.FromFuncRef(source.GetFunction("base")));

        // Act
        var result = instance.GetFunction("asBase").Invoke(context, [WasmValue.FromFuncRef(subFunction)]);

        // Assert
        await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(subFunction);

        // Act & Assert
        var failure = await Assert
            .That(() => instance.GetFunction("asSub").Invoke(context, [WasmValue.FromFuncRef(subFunction)]))
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(failure!.Reason).IsEqualTo(WasmTrapReason.IndirectCallTypeMismatch);
    }
}
