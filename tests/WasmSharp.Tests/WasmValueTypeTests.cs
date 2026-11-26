using System.Reflection;
using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmValueType_UnresolvedTests
{
    [Test]
    public async Task 自身と参照先に異なる未解決の型番号がある_自身の型番号と位置を返す()
    {
        // Arrange
        var direct = new WasmValueType.UnresolvedReference(
            7,
            new WasmFailureLocation(WasmProcessingStage.Decode, 14, null, 1)
        );
        var nested = WasmValueType.UnresolvedTypeReference(
            9,
            true,
            new WasmFailureLocation(WasmProcessingStage.Decode, 21, null, 1)
        );
        var functionType = WasmFunctionType.Create([nested], []);
        // 通常の生成メソッドは両方を同時に設定しないため、優先順位を調べるこのケースだけ非公開コンストラクターを使う。
        var value = (WasmValueType)
            Activator.CreateInstance(
                typeof(WasmValueType),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [WasmValueKind.FuncRef, true, functionType, false, direct],
                culture: null
            )!;

        // Act
        var unresolved = value.Unresolved;

        // Assert
        await Assert.That(unresolved).IsEqualTo(direct);
    }
}

internal class WasmValueType_ReferenceTests
{
    [Test]
    public async Task 数値型を指定する_指定した種類を示して拒否する()
    {
        // Arrange
        const WasmValueKind Kind = WasmValueKind.I32;

        // Act & Assert
        var failure = await Assert
            .That(() => WasmValueType.Reference(Kind, false))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.ParamName).IsEqualTo("kind");
            await Assert.That(failure.ActualValue).IsEqualTo(Kind);
            await Assert.That(failure.Message).Contains("参照型を指定");
        }
    }
}

internal class WasmValueType_BottomReferenceTests
{
    [Test]
    public async Task 数値型を指定する_空のヒープ型として扱えない種類を示して拒否する()
    {
        // Arrange
        const WasmValueKind Kind = WasmValueKind.I64;

        // Act & Assert
        var failure = await Assert
            .That(() => WasmValueType.BottomReference(Kind, true))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.ParamName).IsEqualTo("kind");
            await Assert.That(failure.ActualValue).IsEqualTo(Kind);
            await Assert.That(failure.Message).Contains("参照型を指定");
        }
    }
}

internal class WasmValueType_FunctionReferenceTests
{
    [Test]
    public async Task 関数型がnullである_参照先が必要な引数を示して拒否する()
    {
        // Act & Assert
        var failure = await Assert
            .That(() => WasmValueType.FunctionReference(null!, true))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(failure!.ParamName).IsEqualTo("functionType");
    }
}

internal class WasmValueType_AcceptsTests
{
    [Test]
    [Arguments(WasmValueKind.I31Ref, WasmValueKind.EqRef, true)]
    [Arguments(WasmValueKind.StructRef, WasmValueKind.EqRef, true)]
    [Arguments(WasmValueKind.ArrayRef, WasmValueKind.EqRef, true)]
    [Arguments(WasmValueKind.EqRef, WasmValueKind.AnyRef, true)]
    [Arguments(WasmValueKind.AnyRef, WasmValueKind.EqRef, false)]
    [Arguments(WasmValueKind.ArrayRef, WasmValueKind.StructRef, false)]
    [Arguments(WasmValueKind.AnyRef, WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.ExternRef, WasmValueKind.AnyRef, false)]
    public async Task 抽象参照の宣言型を照合する_同じ階層の上位型に代入できる(
        WasmValueKind source,
        WasmValueKind destination,
        bool expected
    )
    {
        // Arrange
        var target = WasmValueType.Reference(destination, false);
        var actual = WasmValueType.Reference(source, false);

        // Act
        var matches = target.Accepts(actual);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments(WasmValueKind.I31Ref, true, true)]
    [Arguments(WasmValueKind.EqRef, true, true)]
    [Arguments(WasmValueKind.AnyRef, true, true)]
    [Arguments(WasmValueKind.StructRef, false, true)]
    [Arguments(WasmValueKind.ArrayRef, false, true)]
    [Arguments(WasmValueKind.FuncRef, false, false)]
    [Arguments(WasmValueKind.ExternRef, false, false)]
    public async Task 非nullのi31と同じ階層のnullを宣言型に渡す_値の種類とnull可否を照合する(
        WasmValueKind targetKind,
        bool acceptsI31,
        bool acceptsNull
    )
    {
        // Arrange
        var target = WasmValueType.Reference(targetKind, true);
        var required = WasmValueType.Reference(targetKind, false);
        var gcNull = WasmValue.FromNullReference(WasmValueKind.I31Ref);

        // Act
        var present = target.Accepts(WasmValue.FromI31(0));
        var nullable = target.Accepts(gcNull);
        var nonNullable = required.Accepts(gcNull);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(present).IsEqualTo(acceptsI31);
            await Assert.That(nullable).IsEqualTo(acceptsNull);
            await Assert.That(nonNullable).IsFalse();
        }
    }

    [Test]
    [Arguments("0350006000005001006000004F0101600000")] // 先行グループの上位型
    [Arguments("014E0350006000005001006000004F0101600000")] // 同じグループ内の上位型
    public async Task 関数参照を別モジュールの型に代入する_宣言した上位型だけが直接と推移的に受け入れる(string section)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, Convert.FromHexString(section)),
            HostLinkingModuleBinary.Imports(("env", "root", 0, [0]), ("env", "middle", 0, [1]), ("env", "leaf", 0, [2]))
        );
        var expected = WasmModule
            .InspectImports(binary, WasmSpecVersion.Core3)
            .Imports.Cast<WasmImportInfo.Function>()
            .ToArray();
        var actual = WasmModule
            .InspectImports(binary, WasmSpecVersion.Core3)
            .Imports.Cast<WasmImportInfo.Function>()
            .ToArray();
        var root = WasmValueType.FunctionReference(expected[0].Type, false);
        var middle = WasmValueType.FunctionReference(expected[1].Type, false);
        var leaf = WasmValueType.FunctionReference(actual[2].Type, false);
        var unrelated = WasmValueType.FunctionReference(new WasmFunctionType([], []), false);
        var value = WasmValue.FromFuncRef(WasmFunction.CreateHost(actual[2].Type, (_, _) => new WasmResults([])));

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(middle.Accepts(leaf)).IsTrue();
            await Assert.That(root.Accepts(leaf)).IsTrue();
            await Assert.That(root.Accepts(value)).IsTrue();
            await Assert.That(leaf.Accepts(root)).IsFalse();
            await Assert.That(root.Accepts(unrelated)).IsFalse();
            await Assert.That(unrelated.Accepts(value)).IsFalse();
            await Assert.That(root.Accepts(WasmValueType.FunctionReference(actual[2].Type, true))).IsFalse();
            await Assert.That(root).IsNotEqualTo(leaf);
        }
    }

    [Test]
    [Arguments("026000004F0100600000", 0, 1)] // finalな上位型
    [Arguments("0250006000004F01006000017F", 0, 1)] // 結果の個数が異なる
    [Arguments("014E04500060000164024F0100600001640350006000004F01026000017F", 0, 1)] // 結果が後続メンバーの不正な継承に依存する
    [Arguments("036000005001006000004F0101600000", 1, 2)] // 先行グループで型1がfinalな型0を上位型にしている。
    public async Task 検証前のimportに両立しない上位型宣言がある_要求型への代入を拒否する(
        string section,
        byte parentIndex,
        byte childIndex
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, Convert.FromHexString(section)),
            HostLinkingModuleBinary.Imports(("env", "parent", 0, [parentIndex]), ("env", "child", 0, [childIndex]))
        );
        var imports = WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports;
        var expected = WasmValueType.FunctionReference(((WasmImportInfo.Function)imports[0]).Type, false);
        var actual = WasmValueType.FunctionReference(((WasmImportInfo.Function)imports[1]).Type, false);

        // Act
        var accepts = expected.Accepts(actual);

        // Assert
        await Assert.That(accepts).IsFalse();
    }

    [Test]
    public async Task 上位型が循環する検証前のimportを調べる_無関係な関数型への代入を拒否する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, Convert.FromHexString("014E02500101600000500100600000")),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [1]))
        );
        var import = (WasmImportInfo.Function)WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports.Single();
        var actual = WasmValueType.FunctionReference(import.Type, false);
        var expected = WasmValueType.FunctionReference(new WasmFunctionType([], []), false);

        // Act
        var accepts = expected.Accepts(actual);

        // Assert
        await Assert.That(accepts).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 空のヒープ型に値を渡す_null可否に従い実体のある参照を拒否する(bool isNullable)
    {
        // Arrange
        var noFunction = WasmValueType.BottomReference(WasmValueKind.FuncRef, isNullable);
        var noExternal = WasmValueType.BottomReference(WasmValueKind.ExternRef, isNullable);
        var function = WasmFunction.CreateHost(new WasmFunctionType([], []), (_, _) => new WasmResults([]));

        // Act
        var nullFunction = noFunction.Accepts(WasmValue.FromFuncRef(null));
        var presentFunction = noFunction.Accepts(WasmValue.FromFuncRef(function));
        var nullExternal = noExternal.Accepts(WasmValue.FromExternRef(null));
        var presentExternal = noExternal.Accepts(WasmValue.FromExternRef(new object()));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nullFunction).IsEqualTo(isNullable);
            await Assert.That(presentFunction).IsFalse();
            await Assert.That(nullExternal).IsEqualTo(isNullable);
            await Assert.That(presentExternal).IsFalse();
        }
    }
}

internal class WasmValueType_EqualsTests
{
    [Test]
    [Arguments(7, true)]
    [Arguments(8, false)]
    public async Task 異なる位置の未解決参照を比較する_型番号が同じ場合に等しくなる(byte otherIndex, bool expected)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x60, 2, 0x63, 7, 0x63, otherIndex, 0])
        );
        var parameters = WasmModule.Decode(binary, WasmSpecVersion.Core3).FunctionTypeAt(0).ParameterTypes;

        // Act
        var equal = parameters[0].Equals(parameters[1]);

        // Assert
        await Assert.That(equal).IsEqualTo(expected);
    }

    [Test]
    public async Task 別々に宣言した同型の関数参照を比較する_演算子とオブジェクト比較と辞書の結果が一致する()
    {
        // Arrange
        var value = WasmValueType.FunctionReference(new WasmFunctionType([], [WasmValueKind.I32]), false);
        var equivalent = WasmValueType.FunctionReference(new WasmFunctionType([], [WasmValueKind.I32]), false);
        var different = WasmValueType.FunctionReference(new WasmFunctionType([], [WasmValueKind.I64]), false);
        var entries = new Dictionary<WasmValueType, string> { [value] = "callback" };

        // Act
        var equalOperator = value == equivalent;
        var unequalOperator = value != equivalent;
        var differentOperator = value != different;
        var equalObject = value.Equals((object)equivalent);
        var differentObject = value.Equals((object)different);
        var otherObject = value.Equals((object)WasmValueKind.FuncRef);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(equalOperator).IsTrue();
            await Assert.That(unequalOperator).IsFalse();
            await Assert.That(differentOperator).IsTrue();
            await Assert.That(equalObject).IsTrue();
            await Assert.That(differentObject).IsFalse();
            await Assert.That(otherObject).IsFalse();
            await Assert.That(entries[equivalent]).IsEqualTo("callback");
        }
    }
}

internal class WasmValueType_ToStringTests
{
    [Test]
    public async Task 参照先が未解決の型を表示する_元の型番号とnull可否が分かる()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, [1, 0x60, 2, 0x63, 7, 0x64, 8, 0]));
        var parameters = WasmModule.Decode(binary, WasmSpecVersion.Core3).FunctionTypeAt(0).ParameterTypes;

        // Act
        var nullable = parameters[0].ToString();
        var required = parameters[1].ToString();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nullable).IsEqualTo("(ref null 7)");
            await Assert.That(required).IsEqualTo("(ref 8)");
        }
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef, "func", "nofunc")]
    [Arguments(WasmValueKind.ExternRef, "extern", "noextern")]
    [Arguments(WasmValueKind.AnyRef, "any", "none")]
    [Arguments(WasmValueKind.EqRef, "eq", "none")]
    [Arguments(WasmValueKind.I31Ref, "i31", "none")]
    [Arguments(WasmValueKind.StructRef, "struct", "none")]
    [Arguments(WasmValueKind.ArrayRef, "array", "none")]
    public async Task 組み込み参照型を表示する_非nullの型名と階層ごとの底型名になる(
        WasmValueKind kind,
        string heapType,
        string bottomType
    )
    {
        // Arrange
        var required = WasmValueType.Reference(kind, false);
        var nullableBottom = WasmValueType.BottomReference(kind, true);
        var requiredBottom = WasmValueType.BottomReference(kind, false);

        // Act
        var requiredText = required.ToString();
        var nullableBottomText = nullableBottom.ToString();
        var requiredBottomText = requiredBottom.ToString();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(requiredText).IsEqualTo($"(ref {heapType})");
            await Assert.That(nullableBottomText).IsEqualTo($"(ref null {bottomType})");
            await Assert.That(requiredBottomText).IsEqualTo($"(ref {bottomType})");
        }
    }
}
