using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmFunctionType_ConstructorTests
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
        var exception = await Assert
            .That(() => new WasmFunctionType(parameters, results))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("parameters");
            await Assert.That(exception.ActualValue).IsEqualTo(kind);
            await Assert.That(exception.Message).Contains("valueの種類");
        }
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
        var exception = await Assert
            .That(() => new WasmFunctionType(parameters, results))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("results");
            await Assert.That(exception.ActualValue).IsEqualTo(kind);
        }
    }
}

internal class WasmFunctionType_CreateTests
{
    [Test]
    public async Task 短い引数列と結果列から関数型を作る_追加割り当てが公開する四つの型列の大きさに収まる()
    {
        // Arrange
        WasmValueType[] parameters = [WasmValueKind.I32, WasmValueKind.I32];
        WasmValueType[] results = [WasmValueKind.I32];
        var retained = new object[4];
        Action createEmpty = () => retained[0] = WasmFunctionType.Create([], []);
        Action createTyped = () => retained[0] = WasmFunctionType.Create(parameters, results);
        Action allocateTypeArrays = () =>
        {
            retained[0] = new WasmValueType[parameters.Length];
            retained[1] = new WasmValueType[results.Length];
            retained[2] = new WasmValueKind[parameters.Length];
            retained[3] = new WasmValueKind[results.Length];
        };

        // Act
        var emptyAllocation = MeasureAllocation(createEmpty);
        var typedAllocation = MeasureAllocation(createTyped);
        var arrayAllocation = MeasureAllocation(allocateTypeArrays);
        GC.KeepAlive(retained);

        // Assert
        await Assert.That(typedAllocation - emptyAllocation).IsLessThanOrEqualTo(arrayAllocation);

        static long MeasureAllocation(Action allocate)
        {
            allocate();
            var before = GC.GetAllocatedBytesForCurrentThread();
            allocate();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    [Test]
    [Arguments(WasmValueKind.ExnRef, WasmValueKind.I32, "parameters")]
    [Arguments(WasmValueKind.I32, WasmValueKind.ExnRef, "results")]
    public async Task 宣言型に未対応の値の種類を指定する_該当する引数名を報告する(
        WasmValueKind parameter,
        WasmValueKind result,
        string expectedName
    )
    {
        // Arrange
        WasmValueType[] parameters = [parameter];
        WasmValueType[] results = [result];

        // Act & Assert
        var exception = await Assert
            .That(() => WasmFunctionType.Create(parameters, results))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(exception!.ParamName).IsEqualTo(expectedName);
    }
}

internal class WasmFunctionType_GetRefFuncResultTypeTests
{
    [Test]
    public async Task 同じ関数を参照する_Core2はfuncrefでCore3はnull不可の具体的な関数参照型になる()
    {
        // Arrange
        var function = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I64]);

        // Act
        var core2 = function.GetRefFuncResultType(WasmSpecVersion.Core2);
        var core3 = function.GetRefFuncResultType(WasmSpecVersion.Core3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(core2).IsEqualTo((WasmValueType)WasmValueKind.FuncRef);
            await Assert.That(core3).IsEqualTo(WasmValueType.FunctionReference(function, false));
        }
    }
}

internal class WasmFunctionType_ToStringTests
{
    [Test]
    public async Task 相互参照する関数型を表示する_参照先を展開し元の型に戻る位置を示す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    2,
                    0x60,
                    0,
                    0, // 再帰グループより前の関数型
                    0x4E,
                    2,
                    0x60,
                    1,
                    0x63,
                    2,
                    0, // グループの2番目を引数で参照する
                    0x60,
                    0,
                    1,
                    0x64,
                    1, // グループの1番目を結果で参照する
                ]
            ),
            HostLinkingModuleBinary.Imports(("env", "callback", 0, [1]))
        );
        var imports = WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports;
        var first = ((WasmImportInfo.Function)imports.Single()).Type;
        var second = first.ParameterTypes[0].FunctionType!;

        // Act
        var firstDescription = first.ToString();
        var secondDescription = second.ToString();

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(firstDescription)
                .IsEqualTo("(func (param (ref null (func (param ) (result (ref (rec 0)))))) (result ))");
            await Assert
                .That(secondDescription)
                .IsEqualTo("(func (param ) (result (ref (func (param (ref null (rec 1))) (result )))))");
        }
    }

    [Test]
    public async Task 暗黙の単一グループの関数型が自分を結果で参照する_参照先を位置0で表示する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x60, 0, 1, 0x63, 0]),
            HostLinkingModuleBinary.Imports(("env", "callback", 0, [0]))
        );
        var imports = WasmModule.InspectImports(binary, WasmSpecVersion.Core3).Imports;
        var type = ((WasmImportInfo.Function)imports.Single()).Type;

        // Act
        var description = type.ToString();

        // Assert
        await Assert.That(description).IsEqualTo("(func (param ) (result (ref null (rec 0))))");
    }

    [Test]
    public async Task 複数結果を返す関数を引数で参照する_参照先と外側の結果を宣言順に表示する()
    {
        // Arrange
        var callback = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.F32, WasmValueKind.F64]);
        var function = WasmFunctionType.Create(
            [WasmValueType.FunctionReference(callback, true)],
            [WasmValueKind.I64, WasmValueKind.I32]
        );

        // Act
        var description = function.ToString();

        // Assert
        await Assert
            .That(description)
            .IsEqualTo("(func (param (ref null (func (param I32) (result F32 F64)))) (result I64 I32))");
    }
}

internal class WasmFunctionType_DescribeLinkSignatureTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 数値型と関数参照型が混在する_引数と結果のどちらの参照先もリンク診断の表示上限に含める(
        bool useParameters
    )
    {
        // Arrange
        var callback = new WasmFunctionType([.. Enumerable.Repeat(WasmValueKind.I32, 16)], []);
        WasmValueType[] mixed = [WasmValueKind.I64, WasmValueType.FunctionReference(callback, false)];
        WasmValueType[] scalar = [WasmValueKind.F64];
        var function = useParameters ? WasmFunctionType.Create(mixed, scalar) : WasmFunctionType.Create(scalar, mixed);

        // Act
        var description = function.DescribeLinkSignature();

        // Assert
        await Assert.That(description).Contains("I32 …) (result )");
    }
}
