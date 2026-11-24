using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InspectImportsTests
{
    [Test]
    [Arguments("4E01600000", "recursive type group", 11L)]
    [Arguments("4F00600000", "explicit subtype", 11L)]
    [Arguments("5000600000", "explicit subtype", 11L)]
    [Arguments("5F00", "aggregate type", 11L)]
    [Arguments("5E7F00", "aggregate type", 11L)]
    [Arguments("6001630000", "recursive function type", 14L)]
    [Arguments("60016900", "heap type", 13L)] // exn
    [Arguments("60017400", "heap type", 13L)] // noexn
    public async Task 型セクションに未対応の型がある_import調査が機能名と該当する型の位置を返す(
        string definition,
        string feature,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, .. Convert.FromHexString(definition)])
        );

        // Act & Assert
        var failure = await Assert
            .That(() => WasmModule.InspectImports(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.Reason).IsEqualTo(WasmImportInspectionReason.UnsupportedFeature);
            await Assert.That(failure.Feature).IsEqualTo(feature);
            await Assert
                .That(failure.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, 1));
        }
    }

    [Test]
    public async Task 関数importが複数バイトの型番号とnoexternの省略形を使う_型番号を解決しnullを許す外部参照の底型を返す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 0, 0x60, 1, 0x63, 0x80, 0, 1, 0x72]),
            HostLinkingModuleBinary.Imports(("env", "target", 0, [0]), ("env", "apply", 0, [1]))
        );

        // Act
        var imports = WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports;

        // Assert
        var target = (WasmImportInfo.Function)imports[0];
        var apply = (WasmImportInfo.Function)imports[1];
        using (Assert.Multiple())
        {
            await Assert
                .That(apply.Type.ParameterTypes.Single())
                .IsEqualTo(WasmValueType.FunctionReference(target.Type, true));
            await Assert
                .That(apply.Type.ResultTypes.Single())
                .IsEqualTo(WasmValueType.BottomReference(WasmValueKind.ExternRef, true));
        }
    }
}
