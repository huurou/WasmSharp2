using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_ValidateTests
{
    [Test]
    [Arguments("0105016000017E030201000A0601040041010B")]
    [Arguments("0105016000017F030201000A040102000B")]
    [Arguments("0105016000017F030201000A08010600410141020B")]
    [Arguments("01060160017F017E030201000A0601040041010B")]
    public async Task 結果の型や個数が不一致_未実装より先にValidateの失敗を通知する(string sections)
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);
        using var stream = new MemoryStream(bytes);
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];

        // Act & Assert
        foreach (var module in modules)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - 1, 0, 10));
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("01060160017F017F030201000A0601040041010B", "function.parameters", 24L)]
    [Arguments("0105016000017F030201000A08010601017F41010B", "function.locals", 23L)]
    [Arguments("0106016000027F7E030201000A08010600410142020B", "function.results", 24L)]
    public async Task 型は有効だが実行形が未対応_検証の未確認範囲を通知しインスタンス化を拒否する(
        string sections,
        string feature,
        long bodyOffset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);
        using var stream = new MemoryStream(bytes);
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];

        // Act & Assert
        foreach (var module in modules)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmUnsupportedFeatureException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Feature).IsEqualTo(feature);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Validate, bodyOffset, 0, 10));
                await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(1);
                await Assert
                    .That(exception.UnverifiedRanges[0].Stage)
                    .IsEqualTo(WasmProcessingStage.Validate);
                await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(bodyOffset);
                await Assert
                    .That(exception.UnverifiedRanges[0].EndOffset)
                    .IsEqualTo((long)bytes.Length);
            }
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 後半関数の結果型が不正_再試行しても検証失敗しインスタンス化できない()
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create(
                [(0x7F, [0x41, 0x01, 0x0B]), (0x7F, [0x42, 0x02, 0x0B])],
                [("run", 0), ("bad", 1)]
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.Location.FunctionIndex).IsEqualTo(1u);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 後半関数の実行形が範囲外_再試行しても未確認範囲を通知しインスタンス化できない()
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0061736D01000000010A026000017F60017F017F0303020001"
                + "070D020372756E00000362616400010A0B02040041010B040041020B"
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmUnsupportedFeatureException>();
            await Assert.That(exception!.Feature).IsEqualTo("function.parameters");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 49, 1, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(1);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(49L);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 複数関数の検証と再検証が成功_同じモジュールを返す()
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create(
                [(0x7F, [0x41, 0x01, 0x0B]), (0x7E, [0x42, 0x02, 0x0B])],
                [("run", 0), ("RUN", 1), ("alias", 0)]
            )
        );

        // Act
        var first = module.Validate();
        var second = module.Validate();

        // Assert
        await Assert.That(first).IsSameReferenceAs(module);
        await Assert.That(second).IsSameReferenceAs(module);
    }

    [Test]
    public async Task 定義関数がない_検証と再検証で同じモジュールを返す()
    {
        // Arrange
        var module = WasmModule.Decode(Convert.FromHexString("0061736D01000000"));

        // Act
        var first = module.Validate();
        var second = module.Validate();

        // Assert
        await Assert.That(first).IsSameReferenceAs(module);
        await Assert.That(second).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    public async Task Exportの関数indexが存在しない_検証段階のexport位置付き失敗になる(uint index)
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", index)])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, 22, index, 7));
    }

    [Test]
    public async Task Export名が重複_二つ目の宣言位置付き失敗になる()
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0), ("run", 0)])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, 28, 0, 7));
    }

    [Test]
    public async Task 複数関数と別名exportが有効_大文字小文字とUnicode表現を区別して受理する()
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create(
                [(0x7F, [0x41, 0x01, 0x0B]), (0x7E, [0x42, 0x02, 0x0B])],
                [("run", 0), ("RUN", 1), ("é", 0), ("e\u0301", 0), ("", 1)]
            )
        );

        // Act & Assert
        await Assert.That(() => module.Validate()).ThrowsNothing();
    }

    [Test]
    [Arguments("01")]
    [Arguments("FFFFFFFF0F")]
    public async Task 型indexが存在しない_検証段階の関数位置付き失敗になる(string typeIndex)
    {
        // Arrange
        var payloadLength = (1 + typeIndex.Length / 2).ToString("X2");
        var bytes = Convert.FromHexString(
            "0061736D010000000105016000017F03"
                + payloadLength
                + "01"
                + typeIndex
                + "0A0601040041010B"
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - 4, 0, 10));
    }
}
