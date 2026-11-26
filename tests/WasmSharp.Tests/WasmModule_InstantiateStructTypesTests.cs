using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 型番号が異なるモジュールで相互参照する構造体と関数を共有する_同じ関数に構造体のnullを渡せる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var provider = CreateStructFunctionProvider(context);
        var consumer = CreateStructFunctionConsumer("7801");

        // Act
        var target = consumer.Instantiate(context, [provider]).GetFunction("imported");
        var result = target.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.StructRef)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.Single().IsNullReference).IsTrue();
            await Assert.That(result.Values.Single().Kind).IsEqualTo(WasmValueKind.AnyRef);
        }
    }

    [Test]
    [Arguments("7701", false)] // 幅だけが異なる
    [Arguments("7800", false)] // 可変性だけが異なる
    [Arguments("7F01", false)] // 同じi32で異なる格納型を持つ
    [Arguments("7801", true)] // 型とフィールドが一致してもグループの構成が異なる
    public async Task 再帰グループ内の構造体またはグループの宣言数が異なる_関数importのリンクで型不一致を報告する(
        string field,
        bool extraMember
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var provider = CreateStructFunctionProvider(context);
        var consumer = CreateStructFunctionConsumer(field, extraMember);

        // Act & Assert
        var error = await Assert
            .That(() => consumer.Instantiate(context, [provider]))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(error!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
    }

    private static WasmHostModule CreateStructFunctionProvider(WasmExecutionContext context)
    {
        var binary = HostLinkingModuleBinary.Create(
            StructFunctionGroup(false, "7801", false),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Exports(("source", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]))
        );
        var function = WasmModule
            .Decode(binary, WasmSpecVersion.Core3)
            .Validate()
            .Instantiate(context, [])
            .GetFunction("source");
        var provider = new WasmHostModule("host");
        provider.Define("source", function);
        return provider;
    }

    private static WasmModule CreateStructFunctionConsumer(string field, bool extraMember = false)
    {
        var binary = HostLinkingModuleBinary.Create(
            StructFunctionGroup(true, field, extraMember),
            HostLinkingModuleBinary.Imports(("host", "source", 0, [2])),
            HostLinkingModuleBinary.Exports(("imported", 0, 0))
        );
        return WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();
    }

    private static byte[] StructFunctionGroup(bool shifted, string field, bool extraMember)
    {
        // 構造体は同じグループの関数を指すフィールドを持ち、関数はその構造体を受け取って返す。
        var structIndex = shifted ? (byte)1 : (byte)0;
        var functionIndex = (byte)(structIndex + 1);
        byte[] prefix = shifted ? [2, 0x60, 0, 0] : [1];
        byte[] suffix = extraMember ? [0x60, 0, 0] : [];
        return HostLinkingModuleBinary.Section(
            1,
            [
                .. prefix,
                0x4E,
                (byte)(extraMember ? 3 : 2),
                0x5F,
                2,
                0x63,
                functionIndex,
                0,
                .. Convert.FromHexString(field),
                0x60,
                1,
                0x63,
                structIndex,
                1,
                0x63,
                structIndex,
                .. suffix,
            ]
        );
    }
}
