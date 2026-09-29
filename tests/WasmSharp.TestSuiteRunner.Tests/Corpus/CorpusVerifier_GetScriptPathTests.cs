using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusVerifier_GetScriptPathTests
{
    [Test]
    [Arguments("address.wast", "modules/address.json")]
    [Arguments("simd/simd_const.wast", "modules/simd/simd_const.json")]
    public async Task 入力の相対pathを渡す_素材領域内の同じ相対配置のJSONのpathを返す(
        string inputPath,
        string expected
    )
    {
        // Act
        var path = CorpusVerifier.GetScriptPath(inputPath);

        // Assert
        await Assert.That(path).IsEqualTo(expected);
    }
}
