using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false, "008080808010", "memory size")]
    [Arguments(true, "01008080808010", "memory size")]
    [Arguments(false, "058180808080804000", "memory size")]
    [Arguments(true, "050081808080808040", "memory size")]
    [Arguments(false, "04FFFFFFFFFFFFFFFFFF01", "memory size")]
    [Arguments(true, "0581808080108080808010", "size minimum must not be greater than maximum")]
    public async Task Core3のページ数が宣言の制約に違反する_デコード後の検証で宣言位置と原因を示す(
        bool imported,
        string limitsHex,
        string expected
    )
    {
        // Arrange
        var limits = Convert.FromHexString(limitsHex);
        var section = imported
            ? HostLinkingModuleBinary.Imports(("env", "m", 2, limits))
            : HostLinkingModuleBinary.Section(5, [1, .. limits]);
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(section), WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(expected);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.Location.SectionId).IsEqualTo(imported ? (byte)2 : (byte)5);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Core3の64ビットメモリの最小値が2の48乗ページである_定義とimportを割り当てずに検証できる(bool imported)
    {
        // Arrange
        byte[] limits = [0x04, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x40];
        var section = imported
            ? HostLinkingModuleBinary.Imports(("env", "m", 2, limits))
            : HostLinkingModuleBinary.Section(5, [1, .. limits]);
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(section), WasmSpecVersion.Core3);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    public async Task 公式の複数メモリをCore2で検証_従来どおり検証段階で拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(Core3OfficialBinary.MultipleMemories, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("multiple memories");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 13, null, 5));
        }
    }

    [Test]
    public async Task 公式の複数メモリをCore3で検証する_両方のメモリを持つモジュールを受理する()
    {
        // Arrange
        var module = WasmModule.Decode(Core3OfficialBinary.MultipleMemories, WasmSpecVersion.Core3);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments((byte)7)]
    [Arguments((byte)11)]
    public async Task Core3で存在しないメモリをexportまたはデータが参照する_参照元のセクションで検証に失敗する(
        byte sectionId
    )
    {
        // Arrange
        var section =
            sectionId == 7
                ? HostLinkingModuleBinary.Exports(("missing", 2, 2))
                : HostLinkingModuleBinary.Section(11, [1, 2, 2, 0x41, 0, 0x0B, 0]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])),
            HostLinkingModuleBinary.Memories((1, null)),
            section
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown memory 2");
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.Location.SectionId).IsEqualTo(sectionId);
        }
    }
}
