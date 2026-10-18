using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_ElementDropTests
{
    [Test]
    public async Task 指定セグメントを繰り返し破棄する_他segmentと別instanceと転送済み参照を保つ()
    {
        // Arrange
        var reference = TableHandlerFixture.CreateReference(WasmValueKind.ExternRef);
        var table = new WasmTable(WasmValueKind.ExternRef, new(2));
        var function = TableHandlerFixture.CreateFunction([table], [reference], [reference]);
        var instance = function.Instance;
        var peer = new WasmInstance(instance.Module, new(10), [], instance.Globals, [], [table]);
        instance.ElementSegments[1].TryInitialize(table, 0, 0, 1);

        // Act
        var actual = TableHandlerFixture.Execute(Interpreter.ElementDrop, function, 1, []);
        var repeated = TableHandlerFixture.Execute(Interpreter.ElementDrop, function, 1, []);
        var empty = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            1,
            [WasmValue.FromI32(2), WasmValue.FromI32(0), WasmValue.FromI32(0)]
        );
        var nonempty = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            1,
            [WasmValue.FromI32(1), WasmValue.FromI32(0), WasmValue.FromI32(1)]
        );
        var invalidSource = TableHandlerFixture.Execute(
            Interpreter.TableInit,
            function,
            1,
            [WasmValue.FromI32(2), WasmValue.FromI32(1), WasmValue.FromI32(0)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(repeated.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(instance.ElementSegments[0].Length).IsEqualTo(1U);
            await Assert.That(instance.ElementSegments[1].Length).IsEqualTo(0U);
            await Assert.That(peer.ElementSegments[1].Length).IsEqualTo(1U);
            await Assert.That(table.Get(0)).IsEqualTo(reference);
            await Assert.That(table.Get(1)).IsEqualTo(WasmValue.FromExternRef(null));
            await Assert.That(empty.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(empty.ValueCount).IsEqualTo(1);
            await Assert.That(nonempty.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert
                .That(nonempty.Result.TrapReason)
                .IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert.That(invalidSource.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert
                .That(invalidSource.Result.TrapReason)
                .IsEqualTo(WasmTrapReason.TableOutOfBounds);
        }
    }
}
