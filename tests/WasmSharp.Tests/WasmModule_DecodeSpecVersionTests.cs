using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_DecodeSpecVersionTests
{
    [Test]
    public async Task Core3を選択してデコード_検証後のmoduleが同じ仕様版を保持する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0)]);

        // Act
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();

        // Assert
        await Assert.That(module.SpecVersion).IsEqualTo(WasmSpecVersion.Core3);
    }

    [Test]
    public async Task 版を省略してデコード_既定のCore2を保持する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0)]);

        // Act
        var module = WasmModule.Decode(bytes).Validate();

        // Assert
        await Assert.That(module.SpecVersion).IsEqualTo(WasmSpecVersion.Core2);
    }

    [Test]
    public async Task 未定義の版を指定_デコードせず引数の範囲外として拒否する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0)]);

        // Act & Assert
        await Assert.That(() => WasmModule.Decode(bytes, (WasmSpecVersion)2)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
