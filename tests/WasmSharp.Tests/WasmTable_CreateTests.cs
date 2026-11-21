namespace WasmSharp.Tests;

internal class WasmTable_CreateTests
{
    [Test]
    [Arguments(WasmValueKind.I32, 0UL, 1UL << 32)]
    [Arguments(WasmValueKind.I32, 1UL << 32, null)]
    [Arguments(WasmValueKind.I64, 2UL, 1UL)]
    public async Task 要素数の上下限がアドレス型の制約を満たさない_ホストが渡した型を示す引数例外を返す(
        WasmValueKind addressType,
        ulong minimum,
        ulong? maximum
    )
    {
        // Arrange
        var type = new WasmTableType(WasmValueKind.FuncRef, addressType, minimum, maximum);

        // Act & Assert
        var exception = await Assert.That(() => WasmTable.Create(type)).ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("type");
    }
}
