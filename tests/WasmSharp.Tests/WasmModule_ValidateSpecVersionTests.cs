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
    public async Task 公式の複数メモリをCore3で検証_複数メモリの未実装を検証段階で明示する()
    {
        // Arrange
        var bytes = Core3OfficialBinary.MultipleMemories;
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmUnsupportedFeatureException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("multiple memories");
            await Assert.That(exception.Message).Contains("未実装");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 13, null, 5));
            await Assert
                .That(exception.UnverifiedRanges.Select(x => (x.Stage, x.StartOffset, x.EndOffset)))
                .IsEquivalentTo([(WasmProcessingStage.Validate, 0L, (long)bytes.Length)]);
            await Assert.That(exception.UnverifiedRanges.Single().Description).Contains("複数メモリ");
        }

        // Act & Assert
        await Assert.That(() => module.Instantiate(new WasmExecutionContext(), [])).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task 複数メモリで存在しないmemoryをexportする入力をCore3で検証_未実装へ読み替えず検証失敗にする()
    {
        // Arrange
        byte[] bytes = [.. Core3OfficialBinary.MultipleMemories];
        // 2個目のexportが参照するmemory indexを1から2へ変える。
        bytes[^1] = 0x02;
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown memory");
            await Assert.That(exception.Message).Contains("export");
        }
    }
}
