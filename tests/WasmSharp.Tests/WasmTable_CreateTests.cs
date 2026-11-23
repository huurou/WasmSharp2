namespace WasmSharp.Tests;

internal class WasmTable_CreateTests
{
    [Test]
    [Arguments(WasmValueKind.I32, 0UL, 1UL << 32, "仕様上限")]
    [Arguments(WasmValueKind.I32, 1UL << 32, null, "仕様上限")]
    [Arguments(WasmValueKind.I64, 2UL, 1UL, "最小要素数")]
    public async Task 要素数の上下限がアドレス型の制約を満たさない_型を指す引数例外で違反の理由を示す(
        WasmValueKind addressType,
        ulong minimum,
        ulong? maximum,
        string reason
    )
    {
        // Arrange
        var type = new WasmTableType(WasmValueKind.FuncRef, addressType, minimum, maximum);

        // Act & Assert
        var exception = await Assert.That(() => WasmTable.Create(type)).ThrowsExactly<ArgumentException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("type");
            await Assert.That(exception.Message).Contains(reason);
        }
    }

    [Test]
    public async Task テーブル型にnullを渡す_typeを指す引数例外を返す()
    {
        // Act & Assert
        var exception = await Assert.That(() => WasmTable.Create(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("type");
    }
}
