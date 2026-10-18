using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableGrowTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 指定tableを参照で増大する_旧サイズと既存参照を保って共有実体の追加領域を初期化する(
        WasmValueKind kind,
        bool isNull
    )
    {
        // Arrange
        var table = new WasmTable(kind, new(1, 3));
        var original = TableHandlerFixture.CreateReference(kind);
        var added = TableHandlerFixture.CreateReference(kind, isNull);
        table.Set(0, original);
        var other = new WasmTable(kind, new(0));
        var function = TableHandlerFixture.CreateFunction([other, table]);
        var peer = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGrow,
            function,
            1,
            [added, WasmValue.FromI32(2)]
        );
        var shared = TableHandlerFixture.Execute(Interpreter.TableSize, peer, 0, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(1);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Count).IsEqualTo(3U);
            await Assert.That(table.Get(0)).IsEqualTo(original);
            await Assert.That(table.Get(1).Kind).IsEqualTo(kind);
            await Assert.That(table.Get(1)).IsEqualTo(added);
            await Assert.That(table.Get(2)).IsEqualTo(added);
            await Assert.That(shared.Value.AsI32()).IsEqualTo(3);
            await Assert.That(other.Count).IsEqualTo(0U);
        }
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 宣言上限で増分0を指定する_内容を変えず元の要素数を返す(WasmValueKind kind)
    {
        // Arrange
        var table = new WasmTable(kind, new(1, 1));
        var original = TableHandlerFixture.CreateReference(kind);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGrow,
            function,
            0,
            [TableHandlerFixture.CreateReference(kind, true), WasmValue.FromI32(0)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(1);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Count).IsEqualTo(1U);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }

    [Test]
    [Arguments(1U, 1)]
    [Arguments(null, -1)]
    [Arguments(null, int.MinValue)]
    public async Task 宣言上限または符号なし増分が保持上限を超える_内容を変えずマイナス1を返す(
        uint? maximum,
        int delta
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new(1, maximum));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGrow,
            function,
            0,
            [WasmValue.FromExternRef(null), WasmValue.FromI32(delta)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(-1);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Count).IsEqualTo(1U);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }

    [Test]
    public async Task 増大後サイズが配列保持上限を1超える_割当前にマイナス1を返して内容を保つ()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1, uint.MaxValue));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.FuncRef);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGrow,
            function,
            0,
            [WasmValue.FromFuncRef(null), WasmValue.FromI32(Array.MaxLength)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(-1);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Count).IsEqualTo(1U);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }
}
