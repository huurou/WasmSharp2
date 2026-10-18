using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableCopyTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, 0U)]
    [Arguments(WasmValueKind.FuncRef, 1U)]
    [Arguments(WasmValueKind.ExternRef, 0U)]
    [Arguments(WasmValueKind.ExternRef, 1U)]
    public async Task 異なるtableと範囲を指定する_即値とoperandの順を保って同じ参照列をコピーする(
        WasmValueKind kind,
        uint destinationIndex
    )
    {
        // Arrange
        var original = TableHandlerFixture.CreateReference(kind);
        var first = TableHandlerFixture.CreateReference(kind);
        var second = TableHandlerFixture.CreateReference(kind, true);
        var third = TableHandlerFixture.CreateReference(kind);
        var destination = new WasmTable(kind, new(5));
        var source = new WasmTable(kind, new(6));
        destination.TryFill(0, original, 5);
        source.Set(2, first);
        source.Set(3, second);
        source.Set(4, third);
        var tables = destinationIndex == 0 ? new[] { destination, source } : [source, destination];
        var function = TableHandlerFixture.CreateFunction([.. tables]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableCopy,
            function,
            destinationIndex,
            [WasmValue.FromI32(1), WasmValue.FromI32(2), WasmValue.FromI32(3)],
            1 - destinationIndex
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(destination.Get(0)).IsEqualTo(original);
            await Assert.That(destination.Get(1)).IsEqualTo(first);
            await Assert.That(destination.Get(2)).IsEqualTo(second);
            await Assert.That(destination.Get(3)).IsEqualTo(third);
            await Assert.That(destination.Get(4)).IsEqualTo(original);
            await Assert.That(source.Get(2)).IsEqualTo(first);
            await Assert.That(source.Get(3)).IsEqualTo(second);
            await Assert.That(source.Get(4)).IsEqualTo(third);
        }
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(0, 1)]
    public async Task 別添字が同じtableの重複範囲を指す_コピー前の参照列に相当する結果になる(
        int destination,
        int source
    )
    {
        // Arrange
        var values = Enumerable
            .Range(0, 4)
            .Select(x => TableHandlerFixture.CreateReference(WasmValueKind.ExternRef))
            .ToArray();
        var table = new WasmTable(WasmValueKind.ExternRef, new(4));
        table.TryWrite(0, values);
        var function = TableHandlerFixture.CreateFunction([table, table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableCopy,
            function,
            1,
            [WasmValue.FromI32(destination), WasmValue.FromI32(source), WasmValue.FromI32(3)],
            0
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            for (var index = 0; index < 4; index++)
            {
                var expected =
                    index >= destination && index < destination + 3
                        ? values[source + index - destination]
                        : values[index];
                await Assert.That(table.Get((uint)index)).IsEqualTo(expected);
            }
        }
    }

    [Test]
    [Arguments(3, 0, 2)]
    [Arguments(0, 3, 2)]
    [Arguments(-1, 0, 1)]
    [Arguments(0, -1, 1)]
    [Arguments(0, 0, -1)]
    [Arguments(5, 4, 0)]
    public async Task いずれかの範囲が不正である_全operandを除いて位置付きtrapを返し両tableを保つ(
        int destination,
        int source,
        int length
    )
    {
        // Arrange
        var tables = new[]
        {
            new WasmTable(WasmValueKind.ExternRef, new(4)),
            new WasmTable(WasmValueKind.ExternRef, new(4)),
        };
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        tables[0].TryFill(0, original, 4);
        var function = TableHandlerFixture.CreateFunction([.. tables]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableCopy,
            function,
            0,
            [WasmValue.FromI32(destination), WasmValue.FromI32(source), WasmValue.FromI32(length)],
            1
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
            for (uint index = 0; index < 4; index++)
            {
                await Assert.That(tables[0].Get(index)).IsEqualTo(original);
                await Assert.That(tables[1].Get(index)).IsEqualTo(WasmValue.FromExternRef(null));
            }
        }
    }

    [Test]
    public async Task 両方の末尾を開始位置として長さ0を指定する_内容とスタックを保って成功する()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.FuncRef);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableCopy,
            function,
            0,
            [WasmValue.FromI32(1), WasmValue.FromI32(1), WasmValue.FromI32(0)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Get(0)).IsEqualTo(original);
        }
    }
}
