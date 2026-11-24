using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("024020002101200022011A0B2001")] // block内で複数回代入した後、外側で読む
    [Arguments("0340200021010B2001")] // loop内で代入した後、外側で読む
    [Arguments("41000440200021010520011A0B2000")] // thenで初期化し、elseで読む
    [Arguments("410004402000210105200021010B2001")] // 両方の腕で初期化し、ifの後で読む
    [Arguments("002001")] // unreachableの直後で読む
    public async Task 読み取り位置で非nullのlocalが初期化されていない_未初期化を報告する(string instructions)
    {
        // Arrange
        var module = ReferenceControlModule.CreateWithNonNullableLocal(instructions);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("uninitialized local");
    }

    [Test]
    [Arguments("00D4451A")] // unreachable、ref.as_non_null、i32.eqz、drop
    [Arguments("00D500451A")] // unreachable、br_on_null 0、i32.eqz、drop
    public async Task 到達不能な位置で絞り込んだ参照を数値に使う_型不一致を報告する(string instructions)
    {
        // Arrange
        var module = ReferenceControlModule.Create([1, 0x60, 0, 0], Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments("0170", "D0732000D5001A2101D070")] // br_on_nullの後で参照をdropし、残ったfuncrefをlocalへ渡す
    [Arguments("027070", "D0732000D6002101D070D070")] // br_on_non_nullの後で残ったfuncrefをlocalへ渡す
    public async Task 分岐しない経路でラベルの型に広がった値を元の狭い型に戻す_型不一致を報告する(
        string results,
        string instructions
    )
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [1, 0x60, 1, 0x70, .. Convert.FromHexString(results)],
            Convert.FromHexString(instructions),
            locals: [1, 1, 0x73] // (local (ref null nofunc))
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments("4100D41A", "type mismatch")] // i32をref.as_non_nullに渡す
    [Arguments("4100D5001A", "type mismatch")] // i32をbr_on_nullに渡す
    [Arguments("027FD06FD5001A41000B1A", "type mismatch")] // br_on_nullに分岐値のi32が足りない
    [Arguments("D06FD600", "type mismatch")] // 空のラベル
    [Arguments("027FD06FD60041000B1A", "type mismatch")] // ラベルの末尾がi32
    [Arguments("026470D06FD600000B1A", "type mismatch")] // externrefを関数参照のラベルに渡す
    [Arguments("D06FD5011A", "unknown label 1")]
    [Arguments("D06FD601", "unknown label 1")]
    public async Task 参照を検査する命令の入力かラベルが不正である_検証時に原因を報告する(
        string instructions,
        string diagnostic
    )
    {
        // Arrange
        var module = ReferenceControlModule.Create([1, 0x60, 0, 0], Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments("6470")] // (ref func)
    [Arguments("646F")] // (ref extern)
    public async Task 到達不能な位置で絞り込んだ参照を非nullの結果として使う_検証に成功する(string resultType)
    {
        // Arrange
        var module = ReferenceControlModule.Create([1, 0x60, 0, 1, .. Convert.FromHexString(resultType)], [0x00, 0xD4]);

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    public async Task 到達不能な位置で非nullのlocalに代入してから読む_検証に成功する()
    {
        // Arrange
        var module = ReferenceControlModule.CreateWithNonNullableLocal("0021012001"); // unreachable、local.set 1、local.get 1

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }
}
