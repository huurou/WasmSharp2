using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("D06EFB1A", "6E")] // anyをany.convert_externへ渡す
    [Arguments("D06FFB1B", "6F")] // externをextern.convert_anyへ渡す
    [Arguments("4100FB1B", "6F")] // 参照が必要な位置へi32を渡す
    [Arguments("D072FB1A", "646E")] // noexternのnullを変換して非nullのanyを要求する
    [Arguments("D071FB1B", "646F")] // noneのnullを変換して非nullのexternを要求する
    public async Task 関数とglobalの参照変換で入力または結果の型が合わない_型不一致を報告する(string body, string result)
    {
        // Arrange
        var expression = Convert.FromHexString(body);
        var resultType = Convert.FromHexString(result);
        var function = ReferenceControlModule.Create([1, 0x60, 0, 1, .. resultType], expression);
        var global = WasmModule.Decode(
            HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(6, [1, .. resultType, 0, .. expression, 0x0B])),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        using (Assert.Multiple())
        {
            var functionError = await Assert.That(() => function.Validate()).ThrowsExactly<WasmValidateException>();
            var globalError = await Assert.That(() => global.Validate()).ThrowsExactly<WasmValidateException>();
            await Assert.That(functionError!.Message).StartsWith("type mismatch");
            await Assert.That(globalError!.Message).StartsWith("type mismatch");
        }
    }

    [Test]
    [Arguments((byte)0x1A, (byte)0x6E)]
    [Arguments((byte)0x1B, (byte)0x6F)]
    public async Task 到達不能な位置でオペランドを取り出して参照変換する_結果を非nullの参照として検証する(
        byte operation,
        byte result
    )
    {
        // Arrange
        var module = ReferenceControlModule.Create([1, 0x60, 0, 1, 0x64, result], [0x00, 0xFB, operation]);

        // Act & Assert
        await Assert.That(() => module.Validate()).ThrowsNothing();
    }
}
