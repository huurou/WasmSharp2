using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmFunctionType_IsEquivalentToTests
{
    [Test]
    public async Task 比較対象がnullである_比較対象の引数名を含む例外を返す()
    {
        // Arrange
        var type = new WasmFunctionType([], []);

        // Act & Assert
        var exception = await Assert.That(() => type.IsEquivalentTo(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("other");
    }

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
        var allocationSamples = new long[3];

        // Act
        var equivalent = true;
        for (var sample = 0; sample < allocationSamples.Length; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 2000; index++)
            {
                equivalent &= left.IsEquivalentTo(right);
            }
            allocationSamples[sample] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        // 単発の割り当ての影響を除き、同じ比較を繰り返すときの割り当て量を中央値で確認する。
        Array.Sort(allocationSamples);
        var medianAllocated = allocationSamples[allocationSamples.Length / 2];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsTrue();
            await Assert.That(medianAllocated).IsEqualTo(0L);
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

    [Test]
    [Arguments("014E02600000600000", 1)] // 同じグループの二番目
    [Arguments("014E026000006000017F", 0)] // 二番目の関数だけがi32を返す
    [Arguments("01600000", 0)] // 引数も結果もない暗黙の単一グループ
    public async Task 引数と結果が同じでもグループ全体または投影位置が異なる_別の定義型と判定する(string section, byte index)
    {
        // Arrange
        var left = InspectFunctionType("014E02600000600000", 0);
        var right = InspectFunctionType(section, index);

        // Act
        var equivalent = left.IsEquivalentTo(right);
        var sameStructure = left.HasSameStructure(right);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsFalse();
            await Assert.That(sameStructure).IsFalse();
        }
    }

    [Test]
    [Arguments("026000017F4E02600264026300006000016401", 1, "0360017E006000017F4E02600264036301006000016402", 2)]
    [Arguments("016001630000", 0, "024E016000004E016001630100", 1)]
    public async Task 番号の異なるモジュールが同じ再帰グループを宣言する_関数型と参照型が同値になる(
        string leftSection,
        byte leftIndex,
        string rightSection,
        byte rightIndex
    )
    {
        // Arrange
        var left = InspectFunctionType(leftSection, leftIndex);
        var right = InspectFunctionType(rightSection, rightIndex);
        var leftReference = WasmValueType.FunctionReference(left, true);
        var rightReference = WasmValueType.FunctionReference(right, true);

        // Act
        var equivalent = left.IsEquivalentTo(right);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsTrue();
            await Assert.That(leftReference).IsEqualTo(rightReference);
            await Assert.That(leftReference.GetHashCode()).IsEqualTo(rightReference.GetHashCode());
        }
    }

    [Test]
    public async Task 自分を参照する型と先行する同形の型を参照する型を比べる_別の定義型と判定する()
    {
        // Arrange
        var recursive = InspectFunctionType("016000016400", 0);
        var referringToRecursive = InspectFunctionType("0260000164006000016400", 1);

        // Act
        var equivalent = recursive.IsEquivalentTo(referringToRecursive);
        var sameStructure = recursive.HasSameStructure(referringToRecursive);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsFalse();
            await Assert.That(sameStructure).IsFalse();
        }
    }

    [Test]
    [Arguments("01600000", 0, "014F00600000", 0, true)] // finalの省略形
    [Arguments("014E026000005000600000", 0, "014E02600000600000", 0, false)] // 別のメンバーのfinal
    [Arguments("014E0250006000004F0100600000", 0, "014E0250006000004F00600000", 0, false)] // 別のメンバーの上位型
    [Arguments("014E03500060000050006000004F0100600000", 0, "014E03500060000050006000004F0101600000", 0, false)] // 上位型のグループ内位置
    [Arguments("024E02500060000050006000004F0100600000", 2, "0250006000004F0100600000", 1, false)] // グループ外の上位型
    [Arguments("0250006000004F0100600000", 1, "036000017F50006000004F0101600000", 2, true)] // 型番号だけが違う
    public async Task 明示的なサブタイプを含む定義を比較する_グループ全体のfinalと上位型で同値性が決まる(
        string leftSection,
        byte leftIndex,
        string rightSection,
        byte rightIndex,
        bool expected
    )
    {
        // Arrange
        var left = InspectFunctionType(leftSection, leftIndex);
        var right = InspectFunctionType(rightSection, rightIndex);

        // Act
        var equivalent = left.IsEquivalentTo(right);
        var sameStructure = left.HasSameStructure(right);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equivalent).IsEqualTo(expected);
            await Assert.That(sameStructure).IsEqualTo(expected);
        }
    }

    private static WasmFunctionType InspectFunctionType(string typeSection, byte index)
    {
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, Convert.FromHexString(typeSection)),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [index]))
        );
        return ((WasmImportInfo.Function)WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports.Single()).Type;
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
