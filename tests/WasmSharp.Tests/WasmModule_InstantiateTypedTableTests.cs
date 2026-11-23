using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments("6300")]
    [Arguments("6401")]
    [Arguments("6470")]
    public async Task 提供テーブルと要求で関数型やnull可否が異なる_リンク時に型不一致を報告する(string requiredType)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var signature = new WasmFunctionType([], []);
        var elementType = WasmValueType.FunctionReference(signature, isNullable: false);
        var function = WasmFunction.CreateHost(signature, (_, _) => new WasmResults([]));
        var host = new WasmHostModule("env");
        host.Define(
            "t",
            WasmTable.Create(new WasmTableType(elementType, WasmValueKind.I32, 1), WasmValue.FromFuncRef(function))
        );
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(("env", "t", 1, [.. Convert.FromHexString(requiredType), 0, 1]))
        );
        var module = WasmModule.Decode(binary).Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(context, [host]))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            await Assert.That(exception.Message).StartsWith("incompatible import type");
            await Assert.That(exception.Message).Contains(elementType.ToString());
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 型付きテーブルを再exportする_初期値と要素セグメントの関数を両方のinstanceで共有する(bool table64)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var addressType = (byte)(table64 ? 0x7E : 0x7F);
        var offsetOpcode = (byte)(table64 ? 0x42 : 0x41);
        var limits = (byte)(table64 ? 5 : 1);
        var signature = new WasmFunctionType([], []);
        var original = WasmFunction.CreateHost(signature, (_, _) => new WasmResults([]));
        var host = new WasmHostModule("env");
        host.Define(
            "g",
            new WasmGlobal(
                new WasmGlobalType(WasmValueType.FunctionReference(signature, false), false),
                WasmValue.FromFuncRef(original)
            )
        );
        var provider = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [0x64, 0, 0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Section(4, [1, 0x40, 0, 0x64, 0, limits, 2, 3, 0x23, 0, 0x0B]),
                    HostLinkingModuleBinary.Exports(("t", 1, 0), ("replacement", 0, 0)),
                    HostLinkingModuleBinary.Section(9, [1, 6, 0, offsetOpcode, 1, 0x0B, 0x64, 0, 1, 0xD2, 0, 0x0B]),
                    HostLinkingModuleBinary.Code(([], [0x0B]))
                )
            )
            .Validate()
            .Instantiate(context, [host]);
        var registration = new WasmHostModule("provider");
        registration.Define("t", provider.GetTable("t"));
        var consumer = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], []), ([], []), ([addressType], [0x70])),
                    HostLinkingModuleBinary.Imports(("provider", "t", 1, [0x64, 1, limits, 2, 3])),
                    HostLinkingModuleBinary.Functions(2),
                    HostLinkingModuleBinary.Exports(("shared", 1, 0), ("read", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x20, 0, 0x25, 0, 0x0B]))
                )
            )
            .Validate();

        // Act
        var instance = consumer.Instantiate(context, [registration]);
        var read = instance.GetFunction("read");
        var first = read.Invoke(context, [table64 ? WasmValue.FromI64(0) : WasmValue.FromI32(0)]).Values[0];
        var second = read.Invoke(context, [table64 ? WasmValue.FromI64(1) : WasmValue.FromI32(1)]).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetTable("shared")).IsSameReferenceAs(provider.GetTable("t"));
            await Assert.That(first.AsFuncRef()).IsSameReferenceAs(original);
            await Assert.That(second.AsFuncRef()).IsSameReferenceAs(provider.GetFunction("replacement"));
        }
    }

    [Test]
    public async Task 型付きテーブルを定義関数で初期化する_各要素と関数本体の参照が同じ関数を指す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x70])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Section(4, [1, 0x40, 0, 0x64, 0, 0, 2, 0xD2, 0, 0x0B]),
            HostLinkingModuleBinary.Exports(("t", 1, 0), ("reference", 0, 1)),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [0xD2, 0, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary).Validate().Instantiate(context, []);
        var table = instance.GetTable("t");
        var target = instance.GetFunction("reference").Invoke(context, []).Values[0].AsFuncRef();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(target).IsNotNull();
            await Assert.That(table[0].AsFuncRef()).IsSameReferenceAs(target);
            await Assert.That(table[1].AsFuncRef()).IsSameReferenceAs(target);
        }
    }
}
