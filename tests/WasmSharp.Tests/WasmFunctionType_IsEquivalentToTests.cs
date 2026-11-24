namespace WasmSharp.Tests;

internal class WasmFunctionType_IsEquivalentToTests
{
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task 同じコールバック型を引数と結果で参照する_入れ子の比較で追加割当が発生しない(int depth)
    {
        // Arrange
        var left = CreateCallbackType(depth);
        var right = CreateCallbackType(depth);
        left.IsEquivalentTo(right);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var equivalent = true;
        for (var index = 0; index < 2000; index++)
        {
            equivalent &= left.IsEquivalentTo(right);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsTrue();
            await Assert.That(allocated).IsEqualTo(0L);
        }
    }

    [Test]
    public async Task 二つの引数がそれぞれ同じ関数型を参照する_両方の参照先とnull可否で同値性が決まる()
    {
        // Arrange
        var left = CreateBranchedType(WasmValueKind.I32, false);
        var same = CreateBranchedType(WasmValueKind.I32, false);
        var differentResult = CreateBranchedType(WasmValueKind.I64, false);
        var nullableReference = CreateBranchedType(WasmValueKind.I32, true);

        // Act
        var matches = left.IsEquivalentTo(same);
        var matchesDifferentResult = left.IsEquivalentTo(differentResult);
        var matchesNullableReference = left.IsEquivalentTo(nullableReference);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(matches).IsTrue();
            await Assert.That(matchesDifferentResult).IsFalse();
            await Assert.That(matchesNullableReference).IsFalse();
        }
    }

    private static WasmFunctionType CreateCallbackType(int depth)
    {
        var type = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I64]);
        for (var index = 0; index < depth; index++)
        {
            var reference = WasmValueType.FunctionReference(type, false);
            type = WasmFunctionType.Create([reference], [reference]);
        }
        return type;
    }

    private static WasmFunctionType CreateBranchedType(WasmValueKind result, bool isNullable)
    {
        var leaf = new WasmFunctionType([], [result]);
        var leafReference = WasmValueType.FunctionReference(leaf, false);
        var consumer = WasmFunctionType.Create([leafReference], []);
        var producer = WasmFunctionType.Create([], [WasmValueType.FunctionReference(leaf, isNullable)]);
        return WasmFunctionType.Create(
            [WasmValueType.FunctionReference(consumer, false), WasmValueType.FunctionReference(producer, false)],
            []
        );
    }
}
