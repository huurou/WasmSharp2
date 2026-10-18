using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_TableFillTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 指定tableの範囲を埋める_範囲外と他tableを保って同じ参照を設定する(
        WasmValueKind kind,
        bool isNull
    )
    {
        // Arrange
        var original = TableHandlerFixture.CreateReference(kind);
        var value = TableHandlerFixture.CreateReference(kind, isNull);
        var other = new WasmTable(kind, new(4));
        var table = new WasmTable(kind, new(4));
        table.TryFill(0, original, 4);
        var function = TableHandlerFixture.CreateFunction([other, table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableFill,
            function,
            1,
            [WasmValue.FromI32(1), value, WasmValue.FromI32(2)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(table.Get(0)).IsEqualTo(original);
            await Assert.That(table.Get(1)).IsEqualTo(value);
            await Assert.That(table.Get(2)).IsEqualTo(value);
            await Assert.That(table.Get(3)).IsEqualTo(original);
            await Assert
                .That(other.Get(1))
                .IsEqualTo(TableHandlerFixture.CreateReference(kind, true));
        }
    }

    [Test]
    [Arguments(3, 2)]
    [Arguments(-1, 1)]
    [Arguments(0, -1)]
    [Arguments(5, 0)]
    public async Task 範囲が不正である_全operandを除いて位置付きtrapを返し内容を保つ(
        int destination,
        int length
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new(4));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.TryFill(0, original, 4);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableFill,
            function,
            0,
            [
                WasmValue.FromI32(destination),
                WasmValue.FromExternRef(null),
                WasmValue.FromI32(length),
            ]
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
                await Assert.That(table.Get(index)).IsEqualTo(original);
            }
        }
    }

    [Test]
    public async Task 末尾を開始位置として長さ0を指定する_内容とスタックを保って成功する()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new(1));
        var original = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        table.Set(0, original);
        var function = TableHandlerFixture.CreateFunction([table]);

        // Act
        var actual = TableHandlerFixture.Execute(
            Interpreter.TableFill,
            function,
            0,
            [WasmValue.FromI32(1), WasmValue.FromExternRef(null), WasmValue.FromI32(0)]
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
