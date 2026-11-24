namespace WasmSharp.Tests;

internal class WasmFunctionType_StructuralHashCodeTests
{
    private static readonly WasmValueKind[] __numericKinds =
    [
        WasmValueKind.I32,
        WasmValueKind.I64,
        WasmValueKind.F32,
        WasmValueKind.F64,
    ];

    [Test]
    public async Task 同じ型の並びで引数と結果の境界を変える_構造ハッシュが複数に分かれる()
    {
        // Arrange
        WasmValueKind[] kinds = [WasmValueKind.I32, WasmValueKind.I64, WasmValueKind.F32];
        var signatures = Enumerable
            .Range(0, kinds.Length + 1)
            .Select(x => new WasmFunctionType(kinds.AsSpan(0, x), kinds.AsSpan(x)));

        // Act
        var distinctHashCount = signatures.Select(x => x.StructuralHashCode).Distinct().Count();

        // Assert
        await Assert.That(distinctHashCount).IsGreaterThan(1);
    }

    [Test]
    public async Task 引数の型だけを変える_構造ハッシュが複数に分かれる()
    {
        // Arrange
        var signatures = __numericKinds.Select(x => new WasmFunctionType([x], [WasmValueKind.I32]));

        // Act
        var distinctHashCount = signatures.Select(x => x.StructuralHashCode).Distinct().Count();

        // Assert
        await Assert.That(distinctHashCount).IsGreaterThan(1);
    }

    [Test]
    public async Task 結果の型だけを変える_構造ハッシュが複数に分かれる()
    {
        // Arrange
        var signatures = __numericKinds.Select(x => new WasmFunctionType([WasmValueKind.I32], [x]));

        // Act
        var distinctHashCount = signatures.Select(x => x.StructuralHashCode).Distinct().Count();

        // Assert
        await Assert.That(distinctHashCount).IsGreaterThan(1);
    }
}
