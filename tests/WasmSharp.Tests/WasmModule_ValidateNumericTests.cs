using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("67", 1, true)]
    [Arguments("6A", 1, true)]
    [Arguments("41016A", 1, true)]
    [Arguments("420167", 1, false)]
    [Arguments("420141026A", 1, false)]
    [Arguments("410142026A", 1, false)]
    [Arguments("410179", 1, false)]
    [Arguments("41018B", 1, false)]
    [Arguments("430000803F44000000000000F03F92", 1, false)]
    [Arguments("430000803FA7", 1, false)]
    [Arguments("4101FC00", 2, false)]
    [Arguments("FC07", 2, true)]
    public async Task 到達可能な数値命令の入力不足または型不一致_Validateで原因と命令位置を通知しInstantiateを許可しない(
        string instructions,
        int opcodeLength,
        bool missingOperand
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(instructions), 0x1A, 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message)
                .StartsWith(
                    missingOperand ? "type mismatch: 命令の入力値が不足" : "type mismatch: 命令の入力型が一致しません"
                );
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - opcodeLength - 2, 0, 10));
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("420050", (byte)0x7E)]
    [Arguments("420079", (byte)0x7F)]
    [Arguments("430000803F43000000405B", (byte)0x7D)]
    [Arguments("44000000000000F03FFC06", (byte)0x7F)]
    public async Task 数値命令の結果型と関数宣言が不一致_関数endでValidateに失敗する(string instructions, byte resultKind)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [resultKind])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - 1, 0, 10));
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("410141026A", (byte)0x7F, 1)]
    [Arguments("410141026B", (byte)0x7F, 1)]
    [Arguments("410141026C", (byte)0x7F, 1)]
    [Arguments("420142027C", (byte)0x7E, 1)]
    [Arguments("420142027D", (byte)0x7E, 1)]
    [Arguments("420142027E", (byte)0x7E, 1)]
    [Arguments("420079", (byte)0x7E, 1)]
    [Arguments("430000803F8B", (byte)0x7D, 1)]
    [Arguments("44000000000000F03FA0", (byte)0x7C, 1)]
    [Arguments("4201A7", (byte)0x7F, 1)]
    [Arguments("430000803FFC00", (byte)0x7F, 2)]
    [Arguments("44000000000000F03FFC07", (byte)0x7E, 2)]
    public async Task 数値演算をglobal定数式へ置く_Decode後にValidateで禁止命令の位置を通知する(
        string instructions,
        byte resultKind,
        int opcodeLength
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((resultKind, false, [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("constant expression required", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - opcodeLength - 1, null, 6));
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }
}
