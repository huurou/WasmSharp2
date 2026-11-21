using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    public async Task アクティブ要素で64ビットテーブルにi32の開始位置を指定する_検証時に型不一致を報告する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(4, [1, 0x70, 4, 1]),
            HostLinkingModuleBinary.Section(9, [1, 0, 0x41, 0, 0x0B, 0])
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments(WasmSpecVersion.Core3, "23010B", "D0700B", "type mismatch")] // 添字にfuncrefを使う。
    [Arguments(WasmSpecVersion.Core3, "41000B", "23000B", "type mismatch")] // 要素にi32を使う。
    [Arguments(WasmSpecVersion.Core3, "41000B", "23020B", "constant expression required")] // 可変のfuncrefを読む。
    [Arguments(WasmSpecVersion.Core2, "23000B", "D0700B", "unknown global 0")]
    [Arguments(WasmSpecVersion.Core2, "41000B", "23010B", "unknown global 1")]
    public async Task 要素の添字か初期値で定義globalを不正に参照する_仕様版に対応する診断で拒否する(
        WasmSpecVersion version,
        string offset,
        string element,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Tables((0x70, 1, null)),
            HostLinkingModuleBinary.Globals(
                (0x7F, false, [0x41, 0, 0x0B]),
                (0x70, false, [0xD0, 0x70, 0x0B]),
                (0x70, true, [0xD0, 0x70, 0x0B])
            ),
            HostLinkingModuleBinary.Section(
                9,
                [1, 4, .. Convert.FromHexString(offset), 1, .. Convert.FromHexString(element)]
            )
        );
        var module = WasmModule.Decode(bytes, version);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith(diagnostic);
    }

    [Test]
    [Arguments("01000101", "unknown function", 3, null)]
    [Arguments("020141000B0000", "unknown table 1", 0, "要素セグメント")]
    [Arguments("0042000B00", "type mismatch", 3, null)]
    [Arguments("060041000B6F00", "type mismatch", 0, null)]
    [Arguments("057001D06F0B", "type mismatch", 5, null)]
    [Arguments("07700141000B", "type mismatch", 5, null)]
    [Arguments("05700123000B", "unknown global", 3, null)]
    [Arguments("057001D200D1700B", "constant expression required", 5, null)]
    public async Task 要素の添字や型や定数式が不正_Decodeで保持しValidateで拒否してInstantiateを許可しない(
        string segment,
        string prefix,
        int relativeOffset,
        string? expectedDetail
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var section = HostLinkingModuleBinary.Section(9, [1, .. Convert.FromHexString(segment)]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Tables((0x70, 0, null)),
            section
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);
        var segmentOffset = bytes.Length - section.Length + 3;

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(prefix);
            if (expectedDetail is not null)
            {
                await Assert.That(exception.Message).Contains(expectedDetail);
            }
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, segmentOffset + relativeOffset, null, 9));
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素から定義globalかmutableImportを参照する_定数式として拒否する(bool definedGlobal)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            definedGlobal ? [] : HostLinkingModuleBinary.Imports(("env", "g", 3, [0x70, 1])),
            definedGlobal ? HostLinkingModuleBinary.Globals((0x70, false, [0xD0, 0x70, 0x0B])) : [],
            HostLinkingModuleBinary.Section(9, [1, 5, 0x70, 1, 0x23, 0, 0x0B])
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        definedGlobal ? "unknown global" : "constant expression required",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte)9);
        }
    }
}
