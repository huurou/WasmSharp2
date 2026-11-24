namespace WasmSharp.Tests;

internal class WasmFunctionType_HasSameStructureTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 引数か結果に余分な型がある_短い側からも長い側からも構造の不一致を検出する(bool extraParameter)
    {
        // Arrange
        var shorter = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I64]);
        var longer = extraParameter
            ? new WasmFunctionType([WasmValueKind.I32, WasmValueKind.F32], [WasmValueKind.I64])
            : new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I64, WasmValueKind.F32]);

        // Act
        var shortToLong = shorter.HasSameStructure(longer);
        var longToShort = longer.HasSameStructure(shorter);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shortToLong).IsFalse();
            await Assert.That(longToShort).IsFalse();
        }
    }

    [Test]
    [Arguments(null, true)]
    [Arguments(0, false)]
    [Arguments(1, false)]
    [Arguments(2, false)]
    public async Task 三つの関数参照を照合する_すべての参照先のnull可否が一致するときだけ同じ構造と判定する(
        int? nullableBranch,
        bool expected
    )
    {
        // Arrange
        var left = CreateCallbackGroup(null);
        var right = CreateCallbackGroup(nullableBranch);

        // Act
        var equivalent = left.HasSameStructure(right);

        // Assert
        await Assert.That(equivalent).IsEqualTo(expected);
    }

    [Test]
    [Arguments(WasmValueKind.I32, true)]
    [Arguments(WasmValueKind.I64, false)]
    public async Task 片方だけが引数と結果で同じ関数型を参照する_もう一方の両方の参照先を照合する(
        WasmValueKind secondResult,
        bool expected
    )
    {
        // Arrange
        var shared = ReferenceReturning(WasmValueKind.I32);
        var left = WasmFunctionType.Create([shared], [shared]);
        var right = WasmFunctionType.Create([ReferenceReturning(WasmValueKind.I32)], [ReferenceReturning(secondResult)]);

        // Act
        var equivalent = left.HasSameStructure(right);

        // Assert
        await Assert.That(equivalent).IsEqualTo(expected);

        static WasmValueType ReferenceReturning(WasmValueKind result)
        {
            return WasmValueType.FunctionReference(new WasmFunctionType([], [result]), false);
        }
    }

    private static WasmFunctionType CreateCallbackGroup(int? nullableBranch)
    {
        var shared = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I64]);
        var consumer = WasmFunctionType.Create([WasmValueType.FunctionReference(shared, nullableBranch == 0)], []);
        var producer = WasmFunctionType.Create([], [WasmValueType.FunctionReference(shared, nullableBranch == 1)]);
        var selector = WasmFunctionType.Create(
            [WasmValueKind.I32],
            [WasmValueType.FunctionReference(shared, nullableBranch == 2)]
        );
        return WasmFunctionType.Create(
            [WasmValueType.FunctionReference(consumer, false), WasmValueType.FunctionReference(producer, false)],
            [WasmValueType.FunctionReference(selector, false)]
        );
    }
}
