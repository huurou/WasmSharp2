using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableInitTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, 0U)]
    [Arguments(WasmValueKind.FuncRef, 1U)]
    [Arguments(WasmValueKind.ExternRef, 0U)]
    [Arguments(WasmValueKind.ExternRef, 1U)]
    public async Task 要素とtableの添字と範囲を指定する_同じ参照列を転送し供給元を再利用できる(
        WasmValueKind kind,
        uint tableIndex
    )
    {
        // Arrange
        var original = TableHandlerFixture.CreateReference(kind);
        var first = TableHandlerFixture.CreateReference(kind);
        var second = TableHandlerFixture.CreateReference(kind, true);
        var tables = new[] { new WasmTable(kind, new(6)), new WasmTable(kind, new(6)) };
        tables[tableIndex].TryFill(0, original, 6);
        var values = new[] { original, first, second, original };
        var other = new[] { original, original, original, original };
        var function =
            tableIndex == 0
                ? TableHandlerFixture.CreateFunction([.. tables], other, values)
                : TableHandlerFixture.CreateFunction([.. tables], values, other);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            1 - tableIndex,
            [WasmValue.FromI32(3), WasmValue.FromI32(1), WasmValue.FromI32(2)],
            tableIndex
        );
        var repeated = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            1 - tableIndex,
            [WasmValue.FromI32(0), WasmValue.FromI32(1), WasmValue.FromI32(1)],
            tableIndex
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(repeated.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert
                .That(function.Instance.ElementSegments[(int)(1 - tableIndex)].Length)
                .IsEqualTo(4U);
            await Assert.That(tables[tableIndex].Get(0)).IsEqualTo(first);
            await Assert.That(tables[tableIndex].Get(2)).IsEqualTo(original);
            await Assert.That(tables[tableIndex].Get(3)).IsEqualTo(first);
            await Assert.That(tables[tableIndex].Get(4)).IsEqualTo(second);
            await Assert.That(tables[tableIndex].Get(5)).IsEqualTo(original);
            await Assert.That(tables[1 - tableIndex].Get(3)).IsEqualTo(second);
        }
    }

    [Test]
    [Arguments(3, 0, 2)]
    [Arguments(0, 1, 2)]
    [Arguments(-1, 0, 1)]
    [Arguments(0, -1, 1)]
    [Arguments(0, 0, -1)]
    [Arguments(4, 3, 0)]
    public async Task いずれかの範囲が不正である_位置付きtrapを返してtableと供給元を保つ(
        int destination,
        int source,
        int length
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new(4));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.TryFill(0, original, 4);
        var function = TableHandlerFixture.CreateFunction(
            [table],
            [WasmValue.FromExternRef(null), WasmValue.FromExternRef(null)]
        );

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            0,
            [WasmValue.FromI32(destination), WasmValue.FromI32(source), WasmValue.FromI32(length)]
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
            await Assert.That(function.Instance.ElementSegments[0].Length).IsEqualTo(2U);
            for (uint index = 0; index < 4; index++)
            {
                await Assert.That(table.Get(index)).IsEqualTo(original);
            }
        }
    }

    [Test]
    public async Task tableと供給元の末尾から長さ0を転送する_内容と供給元を保って成功する()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.FuncRef);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table], [original, original]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            0,
            [WasmValue.FromI32(1), WasmValue.FromI32(2), WasmValue.FromI32(0)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(function.Instance.ElementSegments[0].Length).IsEqualTo(2U);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }
}
