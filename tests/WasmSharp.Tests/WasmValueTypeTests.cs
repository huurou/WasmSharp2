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
        var nested = WasmValueType.UnresolvedFunctionReference(
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
        var parameters = WasmModule.Decode(binary, WasmSpecVersion.Core3).Types[0].ParameterTypes;

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
    public async Task 関数型と外部参照型のヒープ型を表示する_非nullと空の型に対応した名前になる(
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
