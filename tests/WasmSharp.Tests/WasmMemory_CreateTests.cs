using WasmSharp.Exceptions;

namespace WasmSharp.Tests;

internal class WasmMemory_CreateTests
{
    [Test]
    public async Task 型がnullである_typeを指す引数例外を返す()
    {
        // Act & Assert
        var exception = await Assert.That(() => WasmMemory.Create(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("type");
    }

    [Test]
    [Arguments(0UL, (1UL << 48) + 1)]
    [Arguments(2UL, 1UL)]
    public async Task 生成する64ビットメモリのページ数の上下限が不正である_typeを指す引数例外を返す(
        ulong minimum,
        ulong maximum
    )
    {
        // Arrange
        var type = new WasmMemoryType(WasmValueKind.I64, minimum, maximum);

        // Act & Assert
        var exception = await Assert.That(() => WasmMemory.Create(type)).ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("type");
    }

    [Test]
    public async Task 生成する64ビットメモリの初期ページ数がページ表の保持上限を超える_入力位置のない実装上限例外を返す()
    {
        // Arrange
        var type = new WasmMemoryType(WasmValueKind.I64, 1UL << 32);

        // Act & Assert
        var exception = await Assert.That(() => WasmMemory.Create(type)).ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert.That(exception.Location).IsNull();
            await Assert.That(exception.Message).Contains("ページ表");
        }
    }
}
