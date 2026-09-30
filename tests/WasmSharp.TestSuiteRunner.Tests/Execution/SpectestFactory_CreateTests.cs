using WasmSharp.Exceptions;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class SpectestFactory_CreateTests
{
    [Test]
    public async Task InstantiateのstartでPrintが呼ばれる_現在のmodulecommandへ記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var imports = new WasmImports();
        imports.Add(SpectestFactory.Create(state));
        var module = SpectestModuleFixture.CreateWithStart();
        state.BeginCommand(4);

        // Act
        var instance = module.Instantiate(imports);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance).IsNotNull();
            await Assert.That(state.CurrentCommand).IsEqualTo(new CaseId("a.wast", 4));
            await Assert.That(state.Prints.Length).IsEqualTo(1);
            await Assert.That(state.Prints[0].Function).IsEqualTo("print");
            await Assert.That(state.Prints[0].Arguments.Count).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task Printを呼ぶ_固定型と引数ビットを記録し結果0個で復帰する(int index)
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var host = SpectestFactory.Create(state);
        var instance = SpectestModuleFixture.Instantiate(host);
        state.BeginCommand(3);
        var function = instance.GetFunction(SpectestModuleFixture.functionNames_[index]);
        WasmValue[][] arguments =
        [
            [],
            [WasmValue.FromI32(-1)],
            [WasmValue.FromI64(long.MinValue)],
            [WasmValue.FromF32Bits(0x7F800001)],
            [WasmValue.FromF64Bits(0x8000000000000000)],
            [WasmValue.FromI32(-1), WasmValue.FromF32Bits(0x80000000)],
            [WasmValue.FromF64Bits(0x7FF0000000000001), WasmValue.FromF64Bits(0x8000000000000000)],
        ];
        ValueRecord[][] expected =
        [
            [],
            [new("i32") { Bits = "ffffffff" }],
            [new("i64") { Bits = "8000000000000000" }],
            [new("f32") { Bits = "7f800001" }],
            [new("f64") { Bits = "8000000000000000" }],
            [new("i32") { Bits = "ffffffff" }, new("f32") { Bits = "80000000" }],
            [new("f64") { Bits = "7ff0000000000001" }, new("f64") { Bits = "8000000000000000" }],
        ];

        // Act
        var results = function.Invoke(arguments[index]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(host.Name).IsEqualTo("spectest");
            await Assert
                .That(function.Type.Parameters.SequenceEqual(arguments[index].Select(x => x.Kind)))
                .IsTrue();
            await Assert.That(function.Type.Results.IsEmpty).IsTrue();
            await Assert.That(results.Values.IsEmpty).IsTrue();
            await Assert.That(state.CurrentCommand).IsEqualTo(new CaseId("a.wast", 3));
            await Assert.That(state.Prints.Length).IsEqualTo(1);
            await Assert
                .That(state.Prints[0].Function)
                .IsEqualTo(SpectestModuleFixture.functionNames_[index]);
            await Assert.That(state.Prints[0].Arguments.SequenceEqual(expected[index])).IsTrue();
            await Assert.That(state.CallbackException).IsNull();
        }
    }

    [Test]
    public async Task 固定リソースを生成する_数値globalとtableとmemoryを指定の初期値で提供する()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var instance = SpectestModuleFixture.Instantiate(SpectestFactory.Create(state));

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(instance.GetGlobalResource("global_i32").Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.I32, false));
            await Assert
                .That(instance.GetGlobalResource("global_i64").Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.I64, false));
            await Assert
                .That(instance.GetGlobalResource("global_f32").Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.F32, false));
            await Assert
                .That(instance.GetGlobalResource("global_f64").Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.F64, false));
            await Assert.That(instance.GetGlobal("global_i32").AsI32()).IsEqualTo(666);
            await Assert.That(instance.GetGlobal("global_i64").AsI64()).IsEqualTo(666L);
            await Assert
                .That(instance.GetGlobal("global_f32").AsF32Bits())
                .IsEqualTo(BitConverter.SingleToUInt32Bits(666.6f));
            await Assert
                .That(instance.GetGlobal("global_f64").AsF64Bits())
                .IsEqualTo(BitConverter.DoubleToUInt64Bits(666.6d));
            var table = instance.GetTable("table");
            await Assert.That(table.ElementType).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(table.Count).IsEqualTo(10U);
            await Assert.That(table.MaximumElements).IsEqualTo(20U);
            await Assert
                .That(Enumerable.Range(0, 10).All(x => table.Get((uint)x).AsFuncRef() is null))
                .IsTrue();
            var memory = instance.GetMemory("memory");
            var bytes = new byte[65536];
            memory.Read(0, bytes);
            await Assert.That(memory.PageCount).IsEqualTo(1U);
            await Assert.That(memory.MaximumPages).IsEqualTo(2U);
            await Assert.That(bytes.All(x => x == 0)).IsTrue();
        }
    }

    [Test]
    public async Task 同じ提供元を複数moduleへimportする_全実体とリソースの変更を共有する()
    {
        // Arrange
        var host = SpectestFactory.Create(new ScriptState("a.wast"));
        var first = SpectestModuleFixture.Instantiate(host);
        var reference = first.GetFunction("print");

        // Act
        first.GetMemory("memory").Write(0, [0xAB]);
        var memoryGrown = first.GetMemory("memory").TryGrow(1, out _);
        first.GetTable("table").Set(0, WasmValue.FromFuncRef(reference));
        var tableGrown = first
            .GetTable("table")
            .TryGrow(10, WasmValue.FromFuncRef(reference), out _);
        var second = SpectestModuleFixture.Instantiate(host);

        // Assert
        using (Assert.Multiple())
        {
            foreach (var name in SpectestModuleFixture.functionNames_)
            {
                await Assert
                    .That(second.GetFunction(name))
                    .IsSameReferenceAs(first.GetFunction(name));
            }
            foreach (var name in new[] { "global_i32", "global_i64", "global_f32", "global_f64" })
            {
                await Assert
                    .That(second.GetGlobalResource(name))
                    .IsSameReferenceAs(first.GetGlobalResource(name));
            }
            await Assert
                .That(second.GetMemory("memory"))
                .IsSameReferenceAs(first.GetMemory("memory"));
            await Assert.That(second.GetTable("table")).IsSameReferenceAs(first.GetTable("table"));
            var bytes = new byte[1];
            second.GetMemory("memory").Read(0, bytes);
            await Assert.That(bytes[0]).IsEqualTo((byte)0xAB);
            await Assert.That(memoryGrown).IsTrue();
            await Assert.That(second.GetMemory("memory").PageCount).IsEqualTo(2U);
            await Assert.That(tableGrown).IsTrue();
            await Assert.That(second.GetTable("table").Count).IsEqualTo(20U);
            await Assert
                .That(second.GetTable("table").Get(0).AsFuncRef())
                .IsSameReferenceAs(reference);
            await Assert
                .That(second.GetTable("table").Get(19).AsFuncRef())
                .IsSameReferenceAs(reference);
            await Assert.That(second.GetMemory("memory").TryGrow(1, out _)).IsFalse();
            await Assert
                .That(second.GetTable("table").TryGrow(1, WasmValue.FromFuncRef(null), out _))
                .IsFalse();
        }
    }

    [Test]
    public async Task 別入力の提供元を生成する_全実体を分離してリソースと観測を初期化する()
    {
        // Arrange
        var firstState = new ScriptState("a.wast");
        var first = SpectestModuleFixture.Instantiate(SpectestFactory.Create(firstState));
        firstState.BeginCommand(0);
        first.GetFunction("print").Invoke([]);
        first.GetMemory("memory").Write(0, [0xAB]);
        first.GetMemory("memory").TryGrow(1, out _);
        first.GetTable("table").Set(0, WasmValue.FromFuncRef(first.GetFunction("print")));
        first.GetTable("table").TryGrow(10, WasmValue.FromFuncRef(null), out _);
        var secondState = new ScriptState("b.wast");

        // Act
        var second = SpectestModuleFixture.Instantiate(SpectestFactory.Create(secondState));

        // Assert
        using (Assert.Multiple())
        {
            foreach (var name in SpectestModuleFixture.functionNames_)
            {
                await Assert
                    .That(ReferenceEquals(second.GetFunction(name), first.GetFunction(name)))
                    .IsFalse();
            }
            foreach (var name in new[] { "global_i32", "global_i64", "global_f32", "global_f64" })
            {
                await Assert
                    .That(
                        ReferenceEquals(
                            second.GetGlobalResource(name),
                            first.GetGlobalResource(name)
                        )
                    )
                    .IsFalse();
            }
            await Assert
                .That(ReferenceEquals(second.GetMemory("memory"), first.GetMemory("memory")))
                .IsFalse();
            await Assert
                .That(ReferenceEquals(second.GetTable("table"), first.GetTable("table")))
                .IsFalse();
            var bytes = new byte[65536];
            second.GetMemory("memory").Read(0, bytes);
            await Assert.That(bytes.All(x => x == 0)).IsTrue();
            await Assert.That(second.GetMemory("memory").PageCount).IsEqualTo(1U);
            await Assert.That(second.GetTable("table").Count).IsEqualTo(10U);
            await Assert
                .That(
                    Enumerable
                        .Range(0, 10)
                        .All(x => second.GetTable("table").Get((uint)x).AsFuncRef() is null)
                )
                .IsTrue();
            await Assert.That(secondState.Prints.IsEmpty).IsTrue();
            await Assert.That(secondState.CallbackException).IsNull();
        }
    }

    [Test]
    public async Task Printを順に呼び次のcommandを開始する_呼出順を保持して次のcommandへ記録を持ち越さない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var instance = SpectestModuleFixture.Instantiate(SpectestFactory.Create(state));
        state.BeginCommand(0);

        // Act
        instance.GetFunction("print_i32").Invoke([WasmValue.FromI32(1)]);
        instance.GetFunction("print").Invoke([]);
        var previous = state.Prints;
        state.BeginCommand(2);
        instance.GetFunction("print_i32").Invoke([WasmValue.FromI32(2)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(previous.Select(x => x.Function).SequenceEqual(["print_i32", "print"]))
                .IsTrue();
            await Assert.That(previous[0].Arguments[0].Bits).IsEqualTo("00000001");
            await Assert.That(state.Prints.Length).IsEqualTo(1);
            await Assert.That(state.Prints[0].Arguments[0].Bits).IsEqualTo("00000002");
            await Assert.That(state.CurrentCommand).IsEqualTo(new CaseId("a.wast", 2));
        }
    }

    [Test]
    public async Task Command開始前にcallbackが失敗する_例外実体を記録してそのまま伝播する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var instance = SpectestModuleFixture.Instantiate(SpectestFactory.Create(state));

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction("print").Invoke([]))
            .ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(state.CallbackException).IsSameReferenceAs(exception);
            await Assert.That(state.Prints.IsEmpty).IsTrue();
        }

        // Act
        state.BeginCommand(0);
        instance.GetFunction("print").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(state.CallbackException).IsNull();
            await Assert.That(state.Prints.Length).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 全Printを呼ぶ_標準出力へ出力しない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var instance = SpectestModuleFixture.Instantiate(SpectestFactory.Create(state));
        state.BeginCommand(0);
        var output = TestContext.Current!.Output;
        var before = output.GetStandardOutput();

        // Act
        foreach (var name in SpectestModuleFixture.functionNames_)
        {
            var function = instance.GetFunction(name);
            var arguments = function
                .Type.Parameters.Select(x =>
                    x switch
                    {
                        WasmValueKind.I32 => WasmValue.FromI32(1),
                        WasmValueKind.I64 => WasmValue.FromI64(1),
                        WasmValueKind.F32 => WasmValue.FromF32Bits(0),
                        _ => WasmValue.FromF64Bits(0),
                    }
                )
                .ToArray();
            function.Invoke(arguments);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(output.GetStandardOutput()).IsEqualTo(before);
            await Assert.That(state.Prints.Length).IsEqualTo(7);
        }
    }

    [Test]
    [Arguments("name", WasmInstantiateReason.MissingImport)]
    [Arguments("case", WasmInstantiateReason.MissingImport)]
    [Arguments("kind", WasmInstantiateReason.KindMismatch)]
    [Arguments("function", WasmInstantiateReason.TypeMismatch)]
    [Arguments("global_type", WasmInstantiateReason.TypeMismatch)]
    [Arguments("global_mutability", WasmInstantiateReason.TypeMismatch)]
    [Arguments("table_type", WasmInstantiateReason.TypeMismatch)]
    [Arguments("table_minimum", WasmInstantiateReason.TypeMismatch)]
    [Arguments("table_maximum", WasmInstantiateReason.TypeMismatch)]
    [Arguments("memory_minimum", WasmInstantiateReason.TypeMismatch)]
    [Arguments("memory_maximum", WasmInstantiateReason.TypeMismatch)]
    public async Task 固定提供内容とimport要求が異なる_公開Instantiateがリンク不成立を判定する(
        string issue,
        WasmInstantiateReason reason
    )
    {
        // Arrange
        var host = SpectestFactory.Create(new ScriptState("a.wast"));
        var imports = new WasmImports();
        imports.Add(host);
        var module = SpectestModuleFixture.CreateInvalidImport(issue);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(imports))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(exception!.Reason).IsEqualTo(reason);

        // Assert
        var valid = SpectestModuleFixture.Instantiate(host);
        using (Assert.Multiple())
        {
            await Assert.That(valid.GetMemory("memory").PageCount).IsEqualTo(1U);
            await Assert.That(valid.GetTable("table").Count).IsEqualTo(10U);
        }
    }
}
