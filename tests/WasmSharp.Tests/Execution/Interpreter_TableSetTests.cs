using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableSetTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false, 0U)]
    [Arguments(WasmValueKind.FuncRef, true, 1U)]
    [Arguments(WasmValueKind.ExternRef, false, 1U)]
    [Arguments(WasmValueKind.ExternRef, true, 0U)]
    public async Task 共有tableの要素へ設定する_ホストと別instanceへ同じ型と参照を反映する(
        WasmValueKind kind,
        bool isNull,
        uint tableIndex
    )
    {
        // Arrange
        var tables = new[] { new WasmTable(kind, new(2)), new WasmTable(kind, new(2)) };
        var value = TableHandlerFixture.CreateReference(kind, isNull);
        var original = TableHandlerFixture.CreateReference(kind);
        tables[tableIndex].Set(0, original);
        tables[tableIndex].Set(1, original);
        tables[1 - tableIndex].Set(1, original);
        var function = TableHandlerFixture.CreateFunction([.. tables]);
        var peer = TableHandlerFixture.CreateFunction([tables[tableIndex]]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableSet,
            function,
            tableIndex,
            [WasmValue.FromI32(1), value]
        );
        var shared = TableHandlerFixture.Execute(
            Interpreter.TableGet,
            peer,
            0,
            [WasmValue.FromI32(1)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(tables[tableIndex].Get(1).Kind).IsEqualTo(kind);
            await Assert.That(tables[tableIndex].Get(1)).IsEqualTo(value);
            await Assert.That(shared.Value).IsEqualTo(value);
            await Assert.That(tables[tableIndex].Get(0)).IsEqualTo(original);
            await Assert.That(tables[1 - tableIndex].Get(1)).IsEqualTo(original);
        }
    }

    [Test]
    [Arguments(2)]
    [Arguments(int.MaxValue)]
    [Arguments(int.MinValue)]
    [Arguments(-1)]
    public async Task 要素添字が範囲外である_符号なし境界のtrapと命令位置を返して内容を保つ(
        int index
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new(2));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        var value = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.Set(1, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableSet,
            function,
            0,
            [WasmValue.FromI32(index), value]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(function.FunctionIndex);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(TableHandlerFixture.BYTE_OFFSET);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Count).IsEqualTo(2U);
            await Assert.That(table.Get(0)).IsEqualTo(WasmValue.FromExternRef(null));
            await Assert.That(table.Get(1)).IsEqualTo(original);
        }
    }
}
