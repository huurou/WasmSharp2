using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    public async Task 明示した上位型を持つfinalな関数を宣言する_Core3で検証できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x50, 0, 0x60, 0, 0, 0x4F, 1, 0, 0x60, 0, 0])
        );

        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments("01500100600000", "forward use of type 0 in sub type definition", "先行する型番号", 13L)]
    [Arguments("014E025001016000005000600000", "forward use of type 1 in sub type definition", "先行する型番号", 15L)]
    [Arguments("025001016000005000600000", "unknown type 1", "参照先の型", 13L)]
    [Arguments("014F01FFFFFFFF0F600000", "unknown type 4294967295", "参照先の型", 13L)]
    [Arguments("0250006000004F020000600000", "multiple super types", "1個まで", 17L)]
    [Arguments("026000004F0100600000", "sub type 1 has final super type 0", "finalな関数型を継承", 16L)]
    [Arguments("024F006000004F0100600000", "sub type 1 has final super type 0", "finalな関数型を継承", 18L)]
    [Arguments(
        "03500060000050006000004F01016000017F",
        "sub type 2 does not match super type 1",
        "引数の反変または結果の共変",
        23L
    )] // 同値の型番号0と1
    [Arguments("014E0250010060000050006000016403", "unknown type 3", "参照先の型", 25L)] // 継承関係より先にグループ内の参照を検証
    public async Task 上位型の宣言が不正である_検証時に理由と該当する個数または型番号の位置を報告する(
        string section,
        string diagnostic,
        string detail,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, Convert.FromHexString(section)));
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert.That(exception.Message).Contains(detail);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, offset, null, 1));
        }
    }

    [Test]
    [Arguments("60017F017E", "6000017E")] // 引数の個数
    [Arguments("60017F017E", "60017F00")] // 結果の個数
    [Arguments("60017F017E", "60017E017E")] // i32の引数をi64へ変更
    [Arguments("60017F017E", "60017F017F")] // i64の結果をi32へ変更
    [Arguments("600170016470", "60016470016470")] // nullを許可する引数を非nullに限定
    [Arguments("60016470016470", "6001700170")] // 非nullの結果をnull許可へ変更
    public async Task 関数型の引数の反変または結果の共変を満たさない_上位型との不一致を報告する(string parent, string child)
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
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("sub type 1 does not match super type 0");
    }

    [Test]
    public async Task 後続メンバーの継承関係を使って引数を広げ結果を狭める_再帰グループ全体を検証できる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    1,
                    0x4E,
                    4,
                    .. Convert.FromHexString("500060016403016302"), // 0: (ref 3) -> (ref null 2)
                    .. Convert.FromHexString("4F010060016302016403"), // 1は0を継承: (ref null 2) -> (ref 3)
                    .. Convert.FromHexString("5000600000"), // 2
                    .. Convert.FromHexString("4F0102600000"), // 3は2を継承
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
    [Arguments((byte)2)] // 次のグループ
    [Arguments((byte)3)] // モジュールの範囲外
    public async Task グループの最後を越えた型番号を使う_Decodeが成功しValidateが参照先の位置を報告する(byte index)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x4E, 2, 0x60, 1, 0x64, 1, 0, 0x60, 0, 1, 0x63, index, 0x60, 0, 0])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith($"unknown type {index}");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 22, null, 1));
        }
    }

    [Test]
    public async Task 引数と結果に複数の未解決参照を持つ関数型を読む_検証で最初の参照先とその位置を報告する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, [1, 0x60, 2, 0x63, 7, 0x63, 8, 1, 0x63, 9])),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown type 7");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 14, null, 1));
        }
    }
}
