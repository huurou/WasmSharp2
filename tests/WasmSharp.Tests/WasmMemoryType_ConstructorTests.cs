namespace WasmSharp.Tests;

internal class WasmMemoryType_ConstructorTests
{
    [Test]
    public async Task アドレス型にF32を指定する_引数の範囲外として拒否する()
    {
        // Act & Assert
        await Assert.That(() => new WasmMemoryType(WasmValueKind.F32, 0)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
