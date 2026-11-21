namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_DeconstructTests
{
    [Test]
    public async Task 従来の32ビットメモリのimport情報を分解する_モジュール名と名前とページ数の上下限を返す()
    {
        // Arrange
        var memory = new WasmImportInfo.Memory("env", "m", new WasmLimits(2, 3));

        // Act
        var (moduleName, name, limits) = memory;

        // Assert
        await Assert.That((moduleName, name, limits)).IsEqualTo(("env", "m", new WasmLimits(2, 3)));
    }
}
