using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ImportInspector_InspectTests
{
    [Test]
    public async Task 読取りで保持上限を超える_追加前の位置と読み取った範囲の終端を区別する()
    {
        // Arrange
        using var stream = new ChunkedReadStream(new MemoryStream([1, 2, 3, 4, 5, 6, 7]), 3);

        // Act & Assert
        var exception = await Assert
            .That(() => ImportInspector.Inspect(stream, 5, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImportInspectionReason.ImplementationLimit);
            await Assert.That(exception.Location).IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 3));
            await Assert.That(exception.InnerException).IsTypeOf<WasmImplementationLimitException>();
            await Assert.That(exception.Message).IsEqualTo(exception.InnerException!.Message);
            await Assert
                .That(exception.UnverifiedRanges.Select(x => (x.Stage, x.StartOffset, x.EndOffset)))
                .IsEquivalentTo([(WasmProcessingStage.Decode, 0L, 6L), (WasmProcessingStage.Validate, 0L, 6L)]);
            foreach (var range in exception.UnverifiedRanges)
            {
                await Assert.That(range.Description).Contains("入力終端");
            }
        }
    }

    [Test]
    public async Task 空のcodePayload_成功してもゼロ幅の構文未確認範囲を残す()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create([10, 0]);

        // Act
        var result = ImportInspector.Inspect(bytes, WasmSpecVersion.Core2);

        // Assert
        await Assert.That(result.Imports.IsEmpty).IsTrue();
        await Assert.That(result.UnverifiedRanges.Length).IsEqualTo(2);
        await Assert.That(result.UnverifiedRanges[0].Stage).IsEqualTo(WasmProcessingStage.Decode);
        await Assert.That(result.UnverifiedRanges[0].StartOffset).IsEqualTo(10L);
        await Assert.That(result.UnverifiedRanges[0].EndOffset).IsEqualTo(10L);
        await Assert.That(result.UnverifiedRanges[0].Description).IsEqualTo("このpayloadの構文は未確認です。");
    }

    [Test]
    [Arguments("")]
    [Arguments("020100")]
    [Arguments("000100000100020100000100")]
    public async Task Importがない入力_空一覧と全体の未検証範囲を返す(string sections)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(sections));

        // Act
        var result = ImportInspector.Inspect(bytes, WasmSpecVersion.Core2);

        // Assert
        await Assert.That(result.Imports.IsEmpty).IsTrue();
        await Assert.That(result.UnverifiedRanges.Single().Stage).IsEqualTo(WasmProcessingStage.Validate);
    }

    [Test]
    [Arguments("020100")]
    [Arguments("0302FF")]
    [Arguments("0D00")]
    [Arguments("0A80")]
    [Arguments("0A00FF")]
    [Arguments("000201FF")]
    [Arguments("0A000C0100")]
    public async Task Import後の外枠またはcustom名が破損_部分一覧を返さず失敗する(string tail)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0])),
            Convert.FromHexString(tail)
        );
        WasmImportInspection? result = null;

        // Act & Assert
        await Assert.That(() => result = ImportInspector.Inspect(bytes, WasmSpecVersion.Core2)).Throws<WasmException>();
        await Assert.That(result).IsNull();
    }

    [Test]
    [Arguments(0u)]
    [Arguments(uint.MaxValue)]
    public async Task 関数型を解決できないimport_一覧を返さず失敗する(uint typeIndex)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "f", 0, HostLinkingModuleBinary.Unsigned(typeIndex)))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ImportInspector.Inspect(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImportInspectionReason.UnresolvedType);
            await Assert.That(exception.Message).IsEqualTo("importの関数型添字を解決できません。");
            await Assert
                .That(exception.UnverifiedRanges[0])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Decode,
                        11,
                        bytes.Length,
                        "この位置以降のimport調査が未完了です。"
                    )
                );
            await Assert
                .That(exception.UnverifiedRanges[1])
                .IsEqualTo(
                    new WasmUnverifiedRange(WasmProcessingStage.Validate, 0, bytes.Length, "入力全体の検証が未実施です。")
                );
        }
    }

    [Test]
    [Arguments("01020000")]
    [Arguments("02020000")]
    [Arguments("0208010000037F000080")]
    public async Task 型またはimportの余剰や途中破損_読み飛ばさず失敗する(string sections)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(sections));

        // Act & Assert
        await Assert.That(() => ImportInspector.Inspect(bytes, WasmSpecVersion.Core2)).Throws<WasmException>();
    }

    [Test]
    public async Task 他の全sectionの未解釈payload_読み飛ばした範囲と全体の未検証範囲を返す()
    {
        // Arrange
        var ids = new byte[] { 3, 4, 5, 6, 7, 8, 9, 12, 10, 11 };
        var bytes = HostLinkingModuleBinary.Create([
            HostLinkingModuleBinary.Section(0, [1, 0x61, 0xFF]),
            .. ids.Select(x => HostLinkingModuleBinary.Section(x, [0xFF])),
        ]);

        // Act
        var result = ImportInspector.Inspect(bytes, WasmSpecVersion.Core2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Imports.IsEmpty).IsTrue();
            await Assert.That(result.UnverifiedRanges.Length).IsEqualTo(12);
            await Assert.That(result.UnverifiedRanges[0].Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(result.UnverifiedRanges[0].StartOffset).IsEqualTo(12L);
            await Assert.That(result.UnverifiedRanges[0].EndOffset).IsEqualTo(13L);
            for (var index = 0; index < ids.Length; index++)
            {
                var range = result.UnverifiedRanges[index + 1];
                await Assert.That(range.Stage).IsEqualTo(WasmProcessingStage.Decode);
                await Assert.That(range.StartOffset).IsEqualTo(15L + index * 3);
                await Assert.That(range.EndOffset).IsEqualTo(16L + index * 3);
            }
            await Assert.That(result.UnverifiedRanges[^1].Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(result.UnverifiedRanges[^1].EndOffset).IsEqualTo(bytes.Length);
        }
    }

    [Test]
    public async Task 全種類と同名のimport_宣言順の名前と解決済み要求型を所有する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F], [0x7B, 0x6F])),
            HostLinkingModuleBinary.Imports(
                ("環境", "共有", 3, [0x7B, 1]),
                ("", "", 0, [0]),
                ("env", "memory", 2, HostLinkingModuleBinary.Limits(uint.MaxValue, 0)),
                ("env", "table", 1, [0x6F, .. HostLinkingModuleBinary.Limits(3)]),
                ("環境", "共有", 3, [0x70, 0])
            )
        );

        // Act
        var result = ImportInspector.Inspect(bytes, WasmSpecVersion.Core2);
        Array.Fill(bytes, (byte)0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Imports.Length).IsEqualTo(5);
            await Assert
                .That(
                    result
                        .Imports.Select(x => x.Kind)
                        .SequenceEqual([
                            WasmExternalKind.Global,
                            WasmExternalKind.Function,
                            WasmExternalKind.Memory,
                            WasmExternalKind.Table,
                            WasmExternalKind.Global,
                        ])
                )
                .IsTrue();
            await Assert.That(result.Imports[0].ModuleName).IsEqualTo("環境");
            await Assert.That(result.Imports[0].Name).IsEqualTo("共有");
            await Assert
                .That(((WasmImportInfo.Global)result.Imports[0]).Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.V128, true));
            await Assert.That(result.Imports[1].ModuleName).IsEqualTo("");
            await Assert.That(result.Imports[1].Name).IsEqualTo("");
            var type = ((WasmImportInfo.Function)result.Imports[1]).Type;
            await Assert
                .That(
                    type.Parameters.SequenceEqual(
                        new[]
                        {
                            WasmValueKind.I32,
                            WasmValueKind.I64,
                            WasmValueKind.F32,
                            WasmValueKind.F64,
                            WasmValueKind.V128,
                            WasmValueKind.FuncRef,
                            WasmValueKind.ExternRef,
                        }
                    )
                )
                .IsTrue();
            await Assert.That(type.Results.SequenceEqual(new[] { WasmValueKind.V128, WasmValueKind.ExternRef })).IsTrue();
            await Assert.That(((WasmImportInfo.Memory)result.Imports[2]).Limits).IsEqualTo(new WasmLimits(uint.MaxValue, 0));
            await Assert.That(((WasmImportInfo.Table)result.Imports[3]).ElementType).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(((WasmImportInfo.Table)result.Imports[3]).Limits).IsEqualTo(new WasmLimits(3));
            await Assert
                .That(((WasmImportInfo.Global)result.Imports[4]).Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.FuncRef, false));
            await Assert
                .That(result.UnverifiedRanges.Single())
                .IsEqualTo(
                    new WasmUnverifiedRange(WasmProcessingStage.Validate, 0, bytes.Length, "入力全体の検証が未実施です。")
                );
        }
    }
}
