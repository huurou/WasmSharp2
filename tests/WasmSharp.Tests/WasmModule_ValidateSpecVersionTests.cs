using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
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
