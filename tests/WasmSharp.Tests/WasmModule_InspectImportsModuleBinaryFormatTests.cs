using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InspectImportsTests
{
    [Test]
    public async Task Core3のタグimportを調査する_未対応の機能名と種類の位置を返す()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Imports(("env", "event", 4, [0, 0])));

        // Act & Assert
        var failure = await Assert
            .That(() => WasmModule.InspectImports(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure?.Reason).IsEqualTo(WasmImportInspectionReason.UnsupportedFeature);
            await Assert.That(failure?.Feature).IsEqualTo("tag import");
            await Assert
                .That(failure?.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, bytes.Length - 3, null, 2));
        }
    }

    [Test]
    [Arguments("50005E7F00", "aggregate type", 13L)]
    [Arguments("5E7F00", "aggregate type", 11L)]
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
    public async Task 暗黙の単一型が自分を参照する_importの名前と自己参照を含む要求型を返す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x60, 1, 0x63, 0, 0]),
            HostLinkingModuleBinary.Imports(("env", "callback", 0, [0]))
        );

        // Act
        var import = (WasmImportInfo.Function)WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(import.ModuleName).IsEqualTo("env");
            await Assert.That(import.Name).IsEqualTo("callback");
            await Assert
                .That(import.Type.ParameterTypes.Single())
                .IsEqualTo(WasmValueType.FunctionReference(import.Type, true));
        }
    }

    [Test]
    public async Task 空のグループに続く関数同士が相互参照する_各importの要求型を参照先まで取得できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    2,
                    0x4E,
                    0,
                    0x4E,
                    2,
                    .. Convert.FromHexString("60026401630000"), // consumeはproduceと自分自身を引数に取る。
                    .. Convert.FromHexString("6000016300"), // produceはconsumeを返す。
                ]
            ),
            HostLinkingModuleBinary.Imports(("env", "consume", 0, [0]), ("env", "produce", 0, [1]))
        );

        // Act
        var imports = WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports;

        // Assert
        var consume = ((WasmImportInfo.Function)imports[0]).Type;
        var produce = ((WasmImportInfo.Function)imports[1]).Type;
        WasmValueType[] parameters =
        [
            WasmValueType.FunctionReference(produce, false),
            WasmValueType.FunctionReference(consume, true),
        ];
        using (Assert.Multiple())
        {
            await Assert.That(consume.ParameterTypes.SequenceEqual(parameters)).IsTrue();
            await Assert.That(produce.ResultTypes.Single()).IsEqualTo(WasmValueType.FunctionReference(consume, true));
        }
    }

    [Test]
    public async Task 要求型と同じグループの別の型に不正な参照がある_未解決の番号の位置を報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x4E, 2, 0x60, 0, 0, 0x60, 0, 1, 0x63, 2, 0x60, 0, 0]),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.InspectImports(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImportInspectionReason.UnresolvedType);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 20, null, 1));
        }
    }

    [Test]
    public async Task 要求型が上位型を解決できない関数を参照する_上位型の入力位置を報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, Convert.FromHexString("025001026000006000016400")),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [1]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.InspectImports(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImportInspectionReason.UnresolvedType);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 13, null, 1));
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

    [Test]
    public async Task 関数の引数が相互参照する構造体である_フィールドの格納型と参照先と可変性を取得できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x4E, 2, 0x60, 1, 0x63, 1, 0, 0x5F, 3, 0x64, 0, 0, 0x78, 1, 0x77, 0]),
            HostLinkingModuleBinary.Imports(("env", "callback", 0, [0]))
        );

        // Act
        var import = (WasmImportInfo.Function)WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports.Single();

        // Assert
        var parameter = import.Type.ParameterTypes.Single();
        var structure = parameter.StructType!;
        var fields = structure.Fields;
        using (Assert.Multiple())
        {
            await Assert.That(parameter.Kind).IsEqualTo(WasmValueKind.StructRef);
            await Assert.That(parameter.FunctionType).IsNull();
            await Assert.That(parameter.IsNullable).IsTrue();
            await Assert.That(fields.Length).IsEqualTo(3);
            await Assert.That(fields[0].ValueType).IsEqualTo(WasmValueType.FunctionReference(import.Type, false));
            await Assert.That(fields[0].IsMutable).IsFalse();
            await Assert.That(fields[0].PackedType).IsEqualTo(WasmPackedType.None);
            await Assert.That(fields[1].ValueType).IsEqualTo((WasmValueType)WasmValueKind.I32);
            await Assert.That(fields[1].PackedType).IsEqualTo(WasmPackedType.I8);
            await Assert.That(fields[1].IsMutable).IsTrue();
            await Assert.That(fields[2].PackedType).IsEqualTo(WasmPackedType.I16);
            await Assert.That(fields[2].IsMutable).IsFalse();
            await Assert.That(WasmValueType.StructReference(structure, true)).IsEqualTo(parameter);
            await Assert.That(((WasmValueType)WasmValueKind.EqRef).Accepts(parameter)).IsTrue();
            await Assert.That(parameter.Accepts(WasmValueKind.StructRef)).IsFalse();
            await Assert.That(parameter.Accepts(WasmValue.FromFuncRef(null))).IsFalse();
        }
    }

    [Test]
    public async Task 関数importの型番号が構造体を指す_要求型を取得できないことを報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x5F, 0]),
            HostLinkingModuleBinary.Imports(("env", "callback", 0, [0]))
        );

        // Act & Assert
        var error = await Assert
            .That(() => WasmModule.InspectImports(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmImportInspectionException>();
        await Assert.That(error!.Reason).IsEqualTo(WasmImportInspectionReason.UnresolvedType);
    }
}
