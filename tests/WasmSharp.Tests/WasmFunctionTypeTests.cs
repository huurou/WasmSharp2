namespace WasmSharp.Tests;

public class WasmFunctionType_ConstructorTests
{
    [Test]
    public async Task 空の配列を指定する_引数型と戻り値型の配列が空になる()
    {
        // Arrange
        WasmValueKind[] parameters = [];
        WasmValueKind[] results = [];

        // Act
        var type = new WasmFunctionType(parameters, results);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(type.Parameters.IsDefault).IsFalse();
            await Assert.That(type.Parameters.Length).IsEqualTo(0);
            await Assert.That(type.Results.IsDefault).IsFalse();
            await Assert.That(type.Results.Length).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 構築後に元の配列を変更する_引数型と戻り値型の個数と順序を保持する()
    {
        // Arrange
        WasmValueKind[] expectedParameters =
        [
            WasmValueKind.I32,
            WasmValueKind.I64,
            WasmValueKind.F32,
            WasmValueKind.F64,
            WasmValueKind.V128,
            WasmValueKind.FuncRef,
            WasmValueKind.ExternRef,
            WasmValueKind.I32,
        ];
        WasmValueKind[] expectedResults =
        [
            WasmValueKind.ExternRef,
            WasmValueKind.V128,
            WasmValueKind.I64,
            WasmValueKind.FuncRef,
            WasmValueKind.F64,
            WasmValueKind.F32,
            WasmValueKind.I32,
        ];
        var parameters = expectedParameters.ToArray();
        var results = expectedResults.ToArray();

        // Act
        var type = new WasmFunctionType(parameters, results);
        Array.Fill(parameters, WasmValueKind.ExnRef);
        Array.Fill(results, WasmValueKind.ExnRef);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(type.Parameters.Length).IsEqualTo(8);
            await Assert.That(type.Parameters.SequenceEqual(expectedParameters)).IsTrue();
            await Assert.That(type.Results.Length).IsEqualTo(7);
            await Assert.That(type.Results.SequenceEqual(expectedResults)).IsTrue();
        }
    }

    [Test]
    [Arguments(WasmValueKind.ExnRef)]
    [Arguments((WasmValueKind)(-1))]
    [Arguments((WasmValueKind)int.MaxValue)]
    public async Task 引数の型に対象外の型がある_引数不正として拒否する(WasmValueKind kind)
    {
        // Arrange
        WasmValueKind[] parameters = [WasmValueKind.I32, kind];
        WasmValueKind[] results = [WasmValueKind.I64];

        // Act & Assert
        await Assert
            .That(() => new WasmFunctionType(parameters, results))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(WasmValueKind.ExnRef)]
    [Arguments((WasmValueKind)(-1))]
    [Arguments((WasmValueKind)int.MaxValue)]
    public async Task 戻り値の型に対象外の型がある_引数不正として拒否する(WasmValueKind kind)
    {
        // Arrange
        WasmValueKind[] parameters = [WasmValueKind.I32];
        WasmValueKind[] results = [WasmValueKind.I64, kind];

        // Act & Assert
        await Assert
            .That(() => new WasmFunctionType(parameters, results))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
