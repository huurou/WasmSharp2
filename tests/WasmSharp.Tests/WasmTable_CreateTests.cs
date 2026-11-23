namespace WasmSharp.Tests;

internal class WasmTable_CreateTests
{
    [Test]
    public async Task Null不可のテーブルで初期値を省略または型違いの関数を指定する_最小要素数が0でも拒否する()
    {
        // Arrange
        var signature = new WasmFunctionType([], []);
        var type = new WasmTableType(WasmValueType.FunctionReference(signature, false), WasmValueKind.I32, 0);
        var other = WasmFunction.CreateHost(new WasmFunctionType([WasmValueKind.I32], []), (_, _) => new WasmResults([]));

        // Act & Assert
        var omitted = await Assert.That(() => WasmTable.Create(type)).ThrowsExactly<ArgumentException>();
        var explicitNull = await Assert
            .That(() => WasmTable.Create(type, WasmValue.FromFuncRef(null)))
            .ThrowsExactly<ArgumentException>();
        var wrongSignature = await Assert
            .That(() => WasmTable.Create(type, WasmValue.FromFuncRef(other)))
            .ThrowsExactly<ArgumentException>();
        using (Assert.Multiple())
        {
            await Assert.That(omitted!.ParamName).IsEqualTo("type");
            await Assert.That(explicitNull!.ParamName).IsEqualTo("initialValue");
            await Assert.That(wrongSignature!.ParamName).IsEqualTo("initialValue");
        }
    }

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
