using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 型番号の異なる同型の関数をimportする_引数に渡した定義関数と同じ参照が返る()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var (consumer, provider) = CreateTypedIdentityImport(context, isTargetParameterNullable: false);

        // Act
        var instance = consumer.Instantiate(context, [provider]);
        var returned = instance.GetFunction("run").Invoke(context, []).Values[0].AsFuncRef();

        // Assert
        await Assert.That(returned).IsSameReferenceAs(instance.GetFunction("target"));
    }

    [Test]
    public async Task 引数と結果が指す関数の引数だけnull可否が異なる_import時に型不一致を報告する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var (consumer, provider) = CreateTypedIdentityImport(context, isTargetParameterNullable: true);

        // Act & Assert
        var failure = await Assert
            .That(() => consumer.Instantiate(context, [provider]))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(failure!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
    }

    private static (WasmModule Consumer, WasmHostModule Provider) CreateTypedIdentityImport(
        WasmExecutionContext context,
        bool isTargetParameterNullable
    )
    {
        // 提供側の型1は型0への非null参照を引数に取る。identityは型1への非null参照を受け取り、その参照を返す。
        byte[] leaf = [0x60, 0, 0];
        byte[] providedTarget = [0x60, 1, 0x64, 0, 0];
        byte[] providedIdentity = [0x60, 1, 0x64, 1, 1, 0x64, 1];
        var providerBinary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [3, .. leaf, .. providedTarget, .. providedIdentity]),
            HostLinkingModuleBinary.Functions(2),
            HostLinkingModuleBinary.Exports(("identity", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]))
        );
        var source = WasmModule.Decode(providerBinary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var provider = new WasmHostModule("source");
        provider.Define("identity", source.GetFunction("identity"));

        // 利用側では別の型を先頭に置き、対応する関数型を型1、型2、型3で宣言する。
        byte[] firstType = [0x60, 1, 0x7F, 0];
        var referencePrefix = (byte)(isTargetParameterNullable ? 0x63 : 0x64);
        byte[] requiredTarget = [0x60, 1, referencePrefix, 1, 0];
        byte[] requiredIdentity = [0x60, 1, 0x64, 2, 1, 0x64, 2];
        byte[] run = [0x60, 0, 1, 0x64, 2];
        var consumerBinary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [5, .. firstType, .. leaf, .. requiredTarget, .. requiredIdentity, .. run]),
            HostLinkingModuleBinary.Imports(("source", "identity", 0, [3])),
            HostLinkingModuleBinary.Functions(2, 4),
            HostLinkingModuleBinary.Exports(("target", 0, 1), ("run", 0, 2)),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [0xD2, 1, 0x10, 0, 0x0B]))
        );
        return (WasmModule.Decode(consumerBinary, WasmSpecVersion.Core3).Validate(), provider);
    }
}
