namespace WasmSharp.TestSuiteRunner.Tests;

internal class RunnerPaths_ConstructorTests
{
    [Test]
    [Arguments(true, true, "artifacts/wasm-tools-1.263.0-x86_64-windows/wasm-tools.exe")]
    [Arguments(false, false, "artifacts/wasm-tools-1.263.0-x86_64-linux/wasm-tools")]
    public async Task OSを指定する_パスの大小文字の扱いと変換器の配置が決まる(
        bool isWindows,
        bool samePath,
        string expectedWasmTools
    )
    {
        // Act
        var paths = new RunnerPaths(isWindows);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(string.Equals("sample.wast", "SAMPLE.WAST", paths.Comparison)).IsEqualTo(samePath);
            await Assert.That(paths.WasmTools).IsEqualTo(expectedWasmTools);
        }
    }
}
