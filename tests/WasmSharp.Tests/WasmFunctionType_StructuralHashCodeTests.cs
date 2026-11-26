using WasmSharp.Tests.Fixtures;

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

    [Test]
    [Arguments("4F00600000", "5000600000")] // finalかどうか
    [Arguments("6001630000", "6001640000")] // グループ内の参照のnull可否
    [Arguments("6001640000", "6001640100")] // グループ内の参照先の位置
    [Arguments("5F017800", "5F017700")] // 詰め込みフィールドの幅（i8とi16）
    [Arguments("5F017800", "5F017801")] // i8フィールドの可変性
    public async Task 再帰グループ内で宣言を変える_先頭の同じ関数型の構造ハッシュが複数に分かれる(
        string definition,
        string alternateDefinition
    )
    {
        // Arrange
        byte[][] definitions = [Convert.FromHexString(definition), Convert.FromHexString(alternateDefinition)];
        var hashes = new HashSet<int>();

        // Act
        foreach (var first in definitions)
        {
            foreach (var second in definitions)
            {
                var binary = HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Section(1, [1, 0x4E, 3, 0x60, 0, 0, .. first, .. second])
                );
                hashes.Add(WasmModule.Decode(binary, WasmSpecVersion.Core3).Types[0].StructuralHashCode);
            }
        }

        // Assert
        await Assert.That(hashes.Count).IsGreaterThan(1);
    }
}
