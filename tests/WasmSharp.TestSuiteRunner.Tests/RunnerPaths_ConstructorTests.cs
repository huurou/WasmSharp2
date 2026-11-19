namespace WasmSharp.TestSuiteRunner.Tests;

internal class RunnerPaths_ConstructorTests
{
    [Test]
    [Arguments(true, true, "artifacts/wabt-core2-1.0.42-datacount/Release/wast2json.exe")]
    [Arguments(false, false, "artifacts/wabt-core2-1.0.42-datacount/wast2json")]
    public async Task OSを指定する_パスの大小文字の扱いと変換器の配置が決まる(
        bool isWindows,
        bool samePath,
        string expectedWast2Json
    )
    {
        // Act
        var paths = new RunnerPaths(isWindows);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(string.Equals("sample.wast", "SAMPLE.WAST", paths.Comparison)).IsEqualTo(samePath);
            await Assert.That(paths.Wast2Json).IsEqualTo(expectedWast2Json);
        }
    }
}
