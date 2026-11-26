using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    public async Task 関数と構造体が相互参照する再帰グループを宣言する_各種フィールドを検証できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    1,
                    0x4E,
                    2,
                    0x60,
                    1,
                    0x64,
                    1,
                    0, // 関数の引数は同じグループの構造体
                    0x5F,
                    5,
                    0x63,
                    0,
                    0,
                    0x7E,
                    0,
                    0x7B,
                    1,
                    0x78,
                    1,
                    0x77,
                    0,
                ]
            )
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    public async Task 不変フィールドを具体的な構造体参照へ狭め可変フィールドの型を保って末尾に追加する_継承を検証できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    2,
                    0x50,
                    0,
                    0x5F,
                    2,
                    0x6D,
                    0,
                    0x78,
                    1, // struct { eqref, mut i8 }
                    0x4F,
                    1,
                    0,
                    0x5F,
                    3,
                    0x64,
                    1,
                    0,
                    0x78,
                    1,
                    0x7E,
                    0, // struct { ref 1, mut i8, i64 }
                ]
            )
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments("5F017F00", "5F00")] // 先頭フィールドを削除
    [Arguments("5F017F00", "5F017E00")] // i32からi64
    [Arguments("5F017800", "5F017F00")] // i8からi32
    [Arguments("5F017801", "5F017701")] // 可変i8から可変i16
    [Arguments("5F016B00", "5F016D00")] // 不変structrefから不変eqref
    [Arguments("5F017F00", "5F017F01")] // 可変性の追加
    [Arguments("5F017F01", "5F017F00")] // 可変性の除去
    [Arguments("600000", "5F00")] // 関数型を構造体型で継承
    [Arguments("5F00", "600000")] // 構造体型を関数型で継承
    public async Task 親型と宣言の種類または先頭フィールドが両立しない_上位型との不一致を報告する(
        string parent,
        string child
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [2, 0x50, 0, .. Convert.FromHexString(parent), 0x4F, 1, 0, .. Convert.FromHexString(child)]
            )
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(error!.Message).StartsWith("sub type 1 does not match super type 0");
    }

    [Test]
    public async Task 可変フィールドの値型をeqrefからstructrefへ狭める_不変と可変のフィールドに使える値型を報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x50, 0, 0x5F, 1, 0x6D, 1, 0x4F, 1, 0, 0x5F, 1, 0x6B, 1])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith("sub type 1 does not match super type 0");
            await Assert.That(error.Message).Contains("不変フィールドには親と同じ値型かその部分型");
            await Assert.That(error.Message).Contains("可変フィールドには親と同値の値型");
        }
    }

    [Test]
    [Arguments("025F004F01005F00", "sub type 1 has final super type 0", 15L)]
    [Arguments("025F016301005F00", "unknown type 1", 14L)] // フィールドが後続グループを参照
    [Arguments("015F01630900", "unknown type 9", 14L)] // 範囲外の参照
    public async Task 構造体の親がfinalであるかフィールドの型番号を解決できない_検証で型番号の位置を報告する(
        string section,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, Convert.FromHexString(section)));
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, offset, null, 1));
        }
    }
}
