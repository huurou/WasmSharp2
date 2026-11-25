using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments("base")]
    [Arguments("sub")]
    public async Task 異なる型番号で再帰関数型を要求する_同値型と下位型の関数を直接呼び出して元の関数を再exportする(
        string exportName
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var source = RecursiveFunctionCalls.CreateProvider(context);
        var target = source.GetFunction(exportName);
        var argument = source.GetFunction("sub");
        var provider = new WasmHostModule("source");
        provider.Define("target", target);
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Imports(("source", "target", 0, [1])),
            HostLinkingModuleBinary.Functions(3),
            HostLinkingModuleBinary.Exports(("imported", 0, 0), ("run", 0, 1)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x10, 0, 0x0B]))
        );
        var consumer = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();

        // Act
        var instance = consumer.Instantiate(context, [provider]);
        var result = instance.GetFunction("run").Invoke(context, [WasmValue.FromFuncRef(argument)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetFunction("imported")).IsSameReferenceAs(target);
            await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(argument);
        }
    }

    [Test]
    [Arguments("base")]
    [Arguments("unrelated")]
    public async Task 引数と結果が同じでも要求する下位型を提供していない_インスタンス化で型不一致を報告する(
        string exportName
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var baseFunction = RecursiveFunctionCalls.CreateProvider(context).GetFunction("base");
        var signature = baseFunction.Type;
        var unrelatedType = WasmFunctionType.Create(signature.ParameterTypes.AsSpan(), signature.ResultTypes.AsSpan());
        var provider = new WasmHostModule("source");
        provider.Define("base", baseFunction);
        provider.Define("unrelated", WasmFunction.CreateHost(unrelatedType, (_, arguments) => new WasmResults(arguments)));
        var binary = HostLinkingModuleBinary.Create(
            RecursiveFunctionCalls.CallerTypes(),
            HostLinkingModuleBinary.Imports(("source", exportName, 0, [2])) // $subを要求する
        );
        var consumer = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();

        // Act & Assert
        var failure = await Assert
            .That(() => consumer.Instantiate(context, [provider]))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(failure!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
    }

    [Test]
    [Arguments("table", (byte)1, "0000")] // 最小要素数0のテーブル
    [Arguments("global", (byte)3, "01")] // 可変global
    public async Task 関数参照型のテーブルまたは可変globalをimportする_同値型を受け入れ上位型への変更を拒否する(
        string name,
        byte kind,
        string descriptorTail
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var subFunction = RecursiveFunctionCalls.CreateProvider(context).GetFunction("sub");
        var subReference = WasmValueType.FunctionReference(subFunction.Type, isNullable: true);
        var provider = new WasmHostModule("source");
        provider.Define("table", WasmTable.Create(new WasmTableType(subReference, WasmValueKind.I32, 0)));
        provider.Define("global", new WasmGlobal(new WasmGlobalType(subReference, true), WasmValue.FromFuncRef(null)));
        var sameTypeConsumer = CreateConsumer(2); // $sub
        var supertypeConsumer = CreateConsumer(1); // $base

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => sameTypeConsumer.Instantiate(context, [provider])).ThrowsNothing();
            var failure = await Assert
                .That(() => supertypeConsumer.Instantiate(context, [provider]))
                .ThrowsExactly<WasmInstantiateException>();
            if (failure is not null)
            {
                await Assert.That(failure.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            }
        }

        WasmModule CreateConsumer(byte typeIndex)
        {
            var binary = HostLinkingModuleBinary.Create(
                RecursiveFunctionCalls.CallerTypes(),
                HostLinkingModuleBinary.Imports(
                    ("source", name, kind, [0x63, typeIndex, .. Convert.FromHexString(descriptorTail)])
                )
            );
            return WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();
        }
    }
}
