using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableGetTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false, 0U)]
    [Arguments(WasmValueKind.FuncRef, true, 1U)]
    [Arguments(WasmValueKind.ExternRef, false, 1U)]
    [Arguments(WasmValueKind.ExternRef, true, 0U)]
    public async Task tableと要素を指定する_所属instanceから型と参照同一性を保って取得する(
        WasmValueKind kind,
        bool isNull,
        uint tableIndex
    )
    {
        // Arrange
        var tables = new[] { new WasmTable(kind, new(2)), new WasmTable(kind, new(2)) };
        var value = TableHandlerFixture.CreateReference(kind, isNull);
        var other = TableHandlerFixture.CreateReference(kind);
        tables[tableIndex].Set(1, value);
        tables[1 - tableIndex].Set(1, other);
        var function = TableHandlerFixture.CreateFunction([.. tables]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGet,
            function,
            tableIndex,
            [WasmValue.FromI32(1)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(kind);
            await Assert.That(actual.Value).IsEqualTo(value);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(tables[1 - tableIndex].Get(1)).IsEqualTo(other);
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
        var value = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.Set(1, value);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableGet,
            function,
            0,
            [WasmValue.FromI32(index)]
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
            await Assert.That(table.Get(1)).IsEqualTo(value);
        }
    }
}
