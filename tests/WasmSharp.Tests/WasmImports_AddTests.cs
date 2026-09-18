namespace WasmSharp.Tests;

internal class WasmImports_AddTests
{
    [Test]
    [Arguments("")]
    [Arguments("env")]
    public async Task 同名moduleの非重複itemを追加する_既存登録と取得済みsnapshotを変えない(
        string moduleName
    )
    {
        // Arrange
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var first = new WasmHostModule(moduleName);
        first.Define("", function);
        var global = new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(7));
        var second = new WasmHostModule(moduleName);
        second.Define("new", global);
        var imports = new WasmImports();
        imports.Add(first);
        var before = imports.Snapshot();

        // Act
        imports.Add(second);
        var after = imports.Snapshot();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before[moduleName].Count).IsEqualTo(1);
            await Assert.That(before[moduleName].ContainsKey("new")).IsFalse();
            await Assert.That(after[moduleName].Count).IsEqualTo(2);
            await Assert
                .That(
                    after[moduleName][""] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, function)
                )
                .IsTrue();
            await Assert
                .That(
                    after[moduleName]["new"] is WasmExternalValue.Global g
                        && ReferenceEquals(g.Value, global)
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("env", "Env")]
    [Arguments("\u00e9", "e\u0301")]
    public async Task module名だけが異なる同名itemを追加する_名前を完全一致で区別する(
        string firstName,
        string secondName
    )
    {
        // Arrange
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var first = new WasmHostModule(firstName);
        first.Define("same", function);
        var memory = new WasmMemory(new(0));
        var second = new WasmHostModule(secondName);
        second.Define("same", memory);
        var imports = new WasmImports();

        // Act
        imports.Add(first);
        imports.Add(second);
        var snapshot = imports.Snapshot();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Count).IsEqualTo(2);
            await Assert
                .That(
                    snapshot[firstName]["same"] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, function)
                )
                .IsTrue();
            await Assert
                .That(
                    snapshot[secondName]["same"] is WasmExternalValue.Memory m
                        && ReferenceEquals(m.Value, memory)
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 提供元の一部が重複する_同じ実体や異なる種類でも全件追加せず拒否する(
        bool sameProvider
    )
    {
        // Arrange
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var first = new WasmHostModule("env");
        first.Define("same", function);
        var imports = new WasmImports();
        imports.Add(first);
        var candidate = sameProvider ? first : new WasmHostModule("env");
        candidate.Define("new-first", function);
        if (!sameProvider)
        {
            candidate.Define(
                "same",
                new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7))
            );
        }
        candidate.Define("new-last", new WasmMemory(new(0)));

        // Act & Assert
        await Assert.That(() => imports.Add(candidate)).ThrowsExactly<ArgumentException>();
        var items = imports.Snapshot()["env"];
        using (Assert.Multiple())
        {
            await Assert.That(items.Count).IsEqualTo(1);
            await Assert.That(items.ContainsKey("new-first")).IsFalse();
            await Assert.That(items.ContainsKey("new-last")).IsFalse();
            await Assert
                .That(
                    items["same"] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, function)
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 提供元がnull_登録せず引数不正として拒否する()
    {
        // Arrange
        var imports = new WasmImports();

        // Act & Assert
        var exception = await Assert
            .That(() => imports.Add(null!))
            .ThrowsExactly<ArgumentNullException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("module");
            await Assert.That(imports.Snapshot()).IsEmpty();
        }
    }

    [Test]
    public async Task 引数なしの空提供元を反復追加する_重複itemがないため受け入れる()
    {
        // Arrange
        var imports = new WasmImports();

        // Act
        imports.Add(new WasmHostModule());
        imports.Add(new WasmHostModule());

        // Assert
        await Assert.That(imports.Snapshot().Values.Sum(x => x.Count)).IsEqualTo(0);
    }

    [Test]
    public async Task 追加後に提供元を変更する_4種の登録内容は固定し実体は共有する()
    {
        // Arrange
        var module = new WasmHostModule("env");
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var memory = new WasmMemory(new(0, 1));
        var table = new WasmTable(WasmValueKind.FuncRef, new(0, 1));
        module.Define("function", function);
        module.Define("global", global);
        module.Define("memory", memory);
        module.Define("table", table);
        var imports = new WasmImports();

        // Act
        imports.Add(module);
        module.Define("later", function);
        var snapshot = imports.Snapshot();
        var items = snapshot["env"];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Count).IsEqualTo(1);
            await Assert.That(items.Count).IsEqualTo(4);
            await Assert.That(items.ContainsKey("later")).IsFalse();
            await Assert
                .That(
                    items["function"] is WasmExternalValue.Function f
                        && ReferenceEquals(f.Value, function)
                )
                .IsTrue();
            await Assert
                .That(
                    items["global"] is WasmExternalValue.Global g
                        && ReferenceEquals(g.Value, global)
                )
                .IsTrue();
            await Assert
                .That(
                    items["memory"] is WasmExternalValue.Memory m
                        && ReferenceEquals(m.Value, memory)
                )
                .IsTrue();
            await Assert
                .That(
                    items["table"] is WasmExternalValue.Table t && ReferenceEquals(t.Value, table)
                )
                .IsTrue();
        }
    }
}
