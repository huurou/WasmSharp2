using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_CallIndirectTests
{
    private const long BYTE_OFFSET = 12345678901;
    private static readonly WasmValue sentinel_ = WasmValue.FromF64Bits(0xFFF8123456789ABC);

    [Test]
    [Arguments(2)]
    [Arguments(int.MaxValue)]
    [Arguments(int.MinValue)]
    [Arguments(-1)]
    public async Task 要素添字が範囲外である_他の検査や呼出しを行わず添字付きtrapと呼出し元の位置を返す(
        int selector
    )
    {
        // Arrange
        var called = false;
        var host = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                called = true;
                return new([]);
            }
        );
        var table = new WasmTable(WasmValueKind.FuncRef, new(2));
        table.Set(0, WasmValue.FromFuncRef(host));
        var caller = CreateFunction(
            [new([], []), new([WasmValueKind.I32, WasmValueKind.I64], [])],
            0,
            [table]
        );

        // Act
        var actual = Execute(caller, selector, [WasmValue.FromI32(7), WasmValue.FromI64(9)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert
                .That(actual.Result.TrapDiagnosticKind)
                .IsEqualTo(TrapDiagnosticKind.UndefinedElement);
            await Assert.That(actual.Result.TableElementIndex).IsEqualTo(unchecked((uint)selector));
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(caller.FunctionIndex);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(BYTE_OFFSET);
            await Assert.That(called).IsFalse();
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Caller).IsEqualTo(actual.Before);
            await Assert
                .That(
                    actual.Values.SequenceEqual([
                        sentinel_,
                        WasmValue.FromI32(7),
                        WasmValue.FromI64(9),
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 選んだ要素がnullである_型検査や呼出しを行わず添字付き未初期化trapを返す()
    {
        // Arrange
        var called = false;
        var host = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                called = true;
                return new([]);
            }
        );
        var table = new WasmTable(WasmValueKind.FuncRef, new(2));
        table.Set(0, WasmValue.FromFuncRef(host));
        var caller = CreateFunction(
            [new([], []), new([WasmValueKind.I32, WasmValueKind.I64], [])],
            0,
            [table]
        );

        // Act
        var actual = Execute(caller, 1, [WasmValue.FromI32(7), WasmValue.FromI64(9)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert
                .That(actual.Result.TrapReason)
                .IsEqualTo(WasmTrapReason.UninitializedElement);
            await Assert
                .That(actual.Result.TrapDiagnosticKind)
                .IsEqualTo(TrapDiagnosticKind.Default);
            await Assert.That(actual.Result.TableElementIndex).IsEqualTo(1U);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(caller.FunctionIndex);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(BYTE_OFFSET);
            await Assert.That(called).IsFalse();
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Caller).IsEqualTo(actual.Before);
            await Assert
                .That(
                    actual.Values.SequenceEqual([
                        sentinel_,
                        WasmValue.FromI32(7),
                        WasmValue.FromI64(9),
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 引数型列または結果型列の順序が異なる_深さ上限より先に型不一致trapを返して呼び出さない(
        bool resultMismatch
    )
    {
        // Arrange
        var called = false;
        var parameters = new[] { WasmValueKind.I32, WasmValueKind.I64 };
        var results = new[] { WasmValueKind.I64, WasmValueKind.I32 };
        var host = WasmFunction.CreateHost(
            new(
                resultMismatch ? parameters : [.. parameters.Reverse()],
                resultMismatch ? [.. results.Reverse()] : results
            ),
            _ =>
            {
                called = true;
                return resultMismatch
                    ? new([WasmValue.FromI32(1), WasmValue.FromI64(2)])
                    : new([WasmValue.FromI64(1), WasmValue.FromI32(2)]);
            }
        );
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        table.Set(0, WasmValue.FromFuncRef(host));
        var caller = CreateFunction([new([], []), new(parameters, results)], 0, [table]);

        // Act
        var actual = Execute(caller, 0, [WasmValue.FromI32(7), WasmValue.FromI64(9)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert
                .That(actual.Result.TrapReason)
                .IsEqualTo(WasmTrapReason.IndirectCallTypeMismatch);
            await Assert.That(actual.Result.TableElementIndex).IsNull();
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(caller.FunctionIndex);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(BYTE_OFFSET);
            await Assert.That(called).IsFalse();
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Caller).IsEqualTo(actual.Before);
            await Assert
                .That(
                    actual.Values.SequenceEqual([
                        sentinel_,
                        WasmValue.FromI32(7),
                        WasmValue.FromI64(9),
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 指定tableのホスト関数が構造的に同型である_呼出し元instanceと引数を渡して複数結果の順序を保つ()
    {
        // Arrange
        var expectedType = new WasmFunctionType(
            [WasmValueKind.I32, WasmValueKind.I64],
            [WasmValueKind.I64, WasmValueKind.I32]
        );
        WasmInstance? receivedInstance = null;
        WasmValue[] receivedArguments = [];
        var callDepth = 0;
        var host = WasmFunction.CreateHost(
            new([WasmValueKind.I32, WasmValueKind.I64], [WasmValueKind.I64, WasmValueKind.I32]),
            (instance, arguments) =>
            {
                receivedInstance = instance;
                receivedArguments = arguments.ToArray();
                callDepth = InterpreterContext.Current!.CallDepth;
                return new([
                    WasmValue.FromI64(arguments[1].AsI64() + 1),
                    WasmValue.FromI32(arguments[0].AsI32() + 2),
                ]);
            }
        );
        var selectedTable = new WasmTable(WasmValueKind.FuncRef, new(2));
        selectedTable.Set(1, WasmValue.FromFuncRef(host));
        var caller = CreateFunction(
            [new([], []), expectedType],
            0,
            [new(WasmValueKind.FuncRef, new(0)), selectedTable]
        );

        // Act
        var actual = Execute(
            caller,
            1,
            [WasmValue.FromI32(7), WasmValue.FromI64(9)],
            tableIndex: 1,
            maxCallDepth: 10
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(receivedInstance).IsSameReferenceAs(caller.Instance);
            await Assert
                .That(receivedArguments.SequenceEqual([WasmValue.FromI32(7), WasmValue.FromI64(9)]))
                .IsTrue();
            await Assert.That(callDepth).IsEqualTo(2);
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Caller).IsEqualTo(actual.Before);
            await Assert
                .That(
                    actual.Values.SequenceEqual([
                        sentinel_,
                        WasmValue.FromI64(10),
                        WasmValue.FromI32(9),
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 別moduleの異なる型添字の定義関数が構造的に同型である_元instanceの同じ関数実体へ引数を渡す()
    {
        // Arrange
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(42));
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var memory = new WasmMemory(new(0));
        var callee = CreateFunction(
            [new([WasmValueKind.I32, WasmValueKind.I64], [WasmValueKind.I64, WasmValueKind.I32])],
            0,
            [table],
            [global],
            [memory]
        );
        var selectedTable = new WasmTable(WasmValueKind.FuncRef, new(1));
        selectedTable.Set(0, WasmValue.FromFuncRef(callee));
        var caller = CreateFunction(
            [
                new([], []),
                new([WasmValueKind.I32, WasmValueKind.I64], [WasmValueKind.I64, WasmValueKind.I32]),
            ],
            0,
            [selectedTable]
        );

        // Act
        var actual = Execute(
            caller,
            0,
            [WasmValue.FromI32(7), WasmValue.FromI64(9)],
            maxCallDepth: 10
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.FrameCount).IsEqualTo(2);
            await Assert.That(actual.CallDepth).IsEqualTo(2);
            await Assert.That(actual.Caller).IsEqualTo(actual.Before);
            await Assert.That(actual.Current.Function).IsSameReferenceAs(callee);
            await Assert.That(actual.Current.StackBase).IsEqualTo(1);
            await Assert.That(actual.Current.OperandBase).IsEqualTo(3);
            await Assert.That(actual.Current.Function.Instance).IsSameReferenceAs(callee.Instance);
            await Assert
                .That(actual.Current.Function.Instance.Globals[0])
                .IsSameReferenceAs(global);
            await Assert.That(actual.Current.Function.Instance.Tables[0]).IsSameReferenceAs(table);
            await Assert
                .That(actual.Current.Function.Instance.Memories[0])
                .IsSameReferenceAs(memory);
            await Assert
                .That(
                    actual.Values.SequenceEqual([
                        sentinel_,
                        WasmValue.FromI32(7),
                        WasmValue.FromI64(9),
                    ])
                )
                .IsTrue();
        }
    }

    private static DefinedFunction CreateFunction(
        ImmutableArray<WasmFunctionType> types,
        uint typeIndex,
        ImmutableArray<WasmTable> tables,
        ImmutableArray<WasmGlobal> globals = default,
        ImmutableArray<WasmMemory> memories = default
    )
    {
        var module = new WasmModule(
            types.AsSpan(),
            [new(0, 30, [], []), new(typeIndex, 12345678900, [], [])],
            [],
            12345678910,
            [],
            [],
            [],
            [],
            null
        )
        {
            FunctionCodes = [new([], [], 0), new([], [], 8)],
        };
        return (DefinedFunction)
            new WasmInstance(
                module,
                WasmExecutionOptions.Default,
                [],
                globals.IsDefault ? [] : globals,
                memories.IsDefault ? [] : memories,
                tables
            ).Functions[1];
    }

    private static (
        ExecutionResult Result,
        ImmutableArray<WasmValue> Values,
        ExecutionFrame Before,
        ExecutionFrame Caller,
        ExecutionFrame Current,
        int FrameCount,
        int CallDepth
    ) Execute(
        DefinedFunction caller,
        int selector,
        ReadOnlySpan<WasmValue> arguments,
        uint tableIndex = 0,
        int maxCallDepth = 1
    )
    {
        var context = InterpreterContext.Enter(new(maxCallDepth), out var isOutermost);
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(0, caller.Code.MaxOperandStack, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            var frame = new ExecutionFrame(caller, 0, 0) { Pc = 1 };
            context.PushFrame(frame);
            context.PushValue(sentinel_);
            foreach (var argument in arguments)
            {
                context.PushValue(argument);
            }
            context.PushValue(WasmValue.FromI32(selector));

            var instruction = new Instruction(
                default,
                default,
                BYTE_OFFSET,
                1,
                secondaryIndex: tableIndex
            );
            var result = Interpreter.CallIndirect(context, in instruction);
            return (
                result,
                context.CopyValues(0, context.ValueCount),
                frame,
                context.GetFrame(0),
                context.GetFrame(context.FrameCount - 1),
                context.FrameCount,
                context.CallDepth
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }
    }
}
