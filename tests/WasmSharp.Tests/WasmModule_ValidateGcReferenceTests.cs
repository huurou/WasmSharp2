using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("6E", "D070")] // funcrefをanyrefに返す
    [Arguments("6F", "D071")] // noneのnullをexternrefに返す
    [Arguments("646E", "D06C")] // nullableなi31を非nullのanyに返す
    [Arguments("6C", "D06E")] // anyrefをi31refに返す
    [Arguments("6B", "4100FB1C")] // i31をstructrefに返す
    [Arguments("71", "4100FB1C")] // i31をnullrefに返す
    [Arguments("7F", "D06AFB1E")] // i31.get_uにarrayrefを渡す
    [Arguments("6C", "4200FB1C")] // ref.i31にi64を渡す
    public async Task GC参照の命令または関数結果に代入できない型を渡す_型不一致を報告する(string result, string body)
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [1, 0x60, 0, 1, .. Convert.FromHexString(result)],
            Convert.FromHexString(body)
        );

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(error!.Message).StartsWith("type mismatch");
    }
}
