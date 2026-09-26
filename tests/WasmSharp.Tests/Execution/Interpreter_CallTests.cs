using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_CallTests
{
    [Test]
    public async Task Hostへの入場が深さ上限に達する_呼出し元の関数添字とcall命令の位置を返す()
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
        var instance = ExecutionInstanceFixture.Create(
            [new([], [])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 1001, 0),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    0
                ),
            ],
            [host],
            []
        );
        var context = InterpreterContext.Enter(new(1), out var outermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[1],
                [],
                WasmProcessingStage.Invoke
            );
        }
        finally
        {
            InterpreterContext.Exit(outermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Exhaustion);
            await Assert
                .That(result.ExhaustionReason)
                .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(result.ByteOffset).IsEqualTo(1001L);
            await Assert.That(called).IsFalse();
        }
    }

    [Test]
    public async Task 定義関数を入れ子で呼ぶ_引数と結果を宣言順に受け渡し呼出し側の引数とlocalsを保つ()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x10), out var descriptor);
        var instance = ExecutionInstanceFixture.Create(
            [
                new(
                    [WasmValueKind.I64, WasmValueKind.ExternRef],
                    [WasmValueKind.ExternRef, WasmValueKind.I64]
                ),
                new(
                    [WasmValueKind.ExternRef],
                    [
                        WasmValueKind.I32,
                        WasmValueKind.ExternRef,
                        WasmValueKind.I64,
                        WasmValueKind.I32,
                        WasmValueKind.ExternRef,
                    ]
                ),
            ],
            [
                (
                    0,
                    [new(1, WasmValueKind.I32)],
                    [
                        InstructionFixture.Create(0x20, 1001, 1),
                        InstructionFixture.Create(0x20, 1003, 0),
                        InstructionFixture.Create(0x42, 1005, immediate: WasmValue.FromI64(100)),
                        InstructionFixture.Create(0x21, 1007, 0),
                        InstructionFixture.Create(0x41, 1009, immediate: WasmValue.FromI32(77)),
                        InstructionFixture.Create(0x21, 1011, 2),
                        InstructionFixture.Create(0x0B, 1013),
                    ],
                    3
                ),
                (
                    1,
                    [new(1, WasmValueKind.I32)],
                    [
                        InstructionFixture.Create(0x41, 2001, immediate: WasmValue.FromI32(5)),
                        InstructionFixture.Create(0x21, 2003, 1),
                        InstructionFixture.Create(0x41, 2005, immediate: WasmValue.FromI32(9)),
                        InstructionFixture.Create(0x42, 2007, immediate: WasmValue.FromI64(3)),
                        InstructionFixture.Create(0x20, 2009, 0),
                        InstructionFixture.Create(0x10, 2011, 0),
                        InstructionFixture.Create(0x20, 2013, 1),
                        InstructionFixture.Create(0x20, 2015, 0),
                        InstructionFixture.Create(0x0B, 2017),
                    ],
                    5
                ),
            ],
            [],
            []
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[1],
                [WasmValue.FromExternRef(reference)],
                WasmProcessingStage.Invoke
            );
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.Index);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.Call);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Call);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(5);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(9);
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(result.Values[2].AsI64()).IsEqualTo(3L);
            await Assert.That(result.Values[3].AsI32()).IsEqualTo(5);
            await Assert.That(result.Values[4].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(state).IsEqualTo((0, 0, 0));
        }
    }

    [Test]
    public async Task 別instanceからimportした定義関数を呼ぶ_元instanceの関数表で呼出し先を解決する()
    {
        // Arrange
        var source = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.I32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 1001, 1),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    1
                ),
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x41, 2001, immediate: WasmValue.FromI32(1)),
                        InstructionFixture.Create(0x0B, 2003),
                    ],
                    1
                ),
            ],
            [],
            []
        );
        var importer = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.I32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x41, 1001, immediate: WasmValue.FromI32(2)),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    1
                ),
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 2001, 0),
                        InstructionFixture.Create(0x0B, 2003),
                    ],
                    1
                ),
            ],
            [source.Functions[0]],
            []
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)importer.Functions[2],
                [],
                WasmProcessingStage.Invoke
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Single().AsI32()).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(2)]
    [Arguments(100_000)]
    public async Task 自身を再帰呼出しして上限に達する_CLR再帰せず入場しようとした関数の位置でexhaustionを返し全体を復元する(
        int limit
    )
    {
        // Arrange
        var instance = ExecutionInstanceFixture.Create(
            [new([], [])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 1001, 0),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    0
                ),
            ],
            [],
            []
        );
        var context = InterpreterContext.Enter(new(limit), out var isOutermost);
        ExecutionResult result;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[0],
                [],
                WasmProcessingStage.Invoke
            );
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Exhaustion);
            await Assert
                .That(result.ExhaustionReason)
                .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(result.Limit).IsEqualTo(limit);
            await Assert.That(result.FunctionIndex).IsEqualTo(0U);
            await Assert
                .That(result.ByteOffset)
                .IsEqualTo(ExecutionInstanceFixture.GetBodyOffset(0));
            await Assert.That(state).IsEqualTo((0, 0, 0));
        }
    }

    [Test]
    public async Task 上限2で呼出しを繰り返す_戻るたびに深さを解放して最後の結果を返す()
    {
        // Arrange
        var instance = ExecutionInstanceFixture.Create(
            [new([], [WasmValueKind.I32])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x41, 1001, immediate: WasmValue.FromI32(1)),
                        InstructionFixture.Create(0x0B, 1003),
                    ],
                    1
                ),
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 2001, 0),
                        InstructionFixture.Create(0x1A, 2003),
                        InstructionFixture.Create(0x10, 2004, 0),
                        InstructionFixture.Create(0x1A, 2006),
                        InstructionFixture.Create(0x10, 2007, 0),
                        InstructionFixture.Create(0x0B, 2009),
                    ],
                    1
                ),
            ],
            [],
            []
        );
        var context = InterpreterContext.Enter(new(2), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                (DefinedFunction)instance.Functions[1],
                [],
                WasmProcessingStage.Invoke
            );
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Single().AsI32()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 呼出し先の必要容量が保持上限を超える_入口の処理段階と呼出し先の位置で実装上限とし外側を復元する()
    {
        // Arrange
        var instance = ExecutionInstanceFixture.Create(
            [new([], [])],
            [
                (0, [], [InstructionFixture.Create(0x0B, 1001)], int.MaxValue),
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 2001, 0),
                        InstructionFixture.Create(0x0B, 2003),
                    ],
                    0
                ),
            ],
            [],
            []
        );
        (int Frames, int Values, int Depth, WasmProcessingStage Stage) state = default;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var context = InterpreterContext.Enter(new(10), out var isOutermost);
                try
                {
                    context.Stage = WasmProcessingStage.Invoke;
                    try
                    {
                        Interpreter.Run(
                            context,
                            (DefinedFunction)instance.Functions[1],
                            [],
                            WasmProcessingStage.Instantiate
                        );
                    }
                    finally
                    {
                        state = (
                            context.FrameCount,
                            context.ValueCount,
                            context.CallDepth,
                            context.Stage
                        );
                    }
                }
                finally
                {
                    context.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                }
            })
            .ThrowsExactly<WasmImplementationLimitException>();

        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(
                        WasmProcessingStage.Instantiate,
                        ExecutionInstanceFixture.GetBodyOffset(0),
                        0
                    )
                );
            await Assert.That(state).IsEqualTo((0, 0, 0, WasmProcessingStage.Invoke));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ホスト関数の結果を受け取る_正常時だけ後続命令を実行し状態を復元する(
        bool invalid
    )
    {
        // Arrange
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var host = WasmFunction.CreateHost(
            new([], [WasmValueKind.I32]),
            _ => invalid ? new([]) : new([WasmValue.FromI32(42)])
        );
        var instance = ExecutionInstanceFixture.Create(
            [new([], [])],
            [
                (
                    0,
                    [],
                    [
                        InstructionFixture.Create(0x10, 1001, 0),
                        InstructionFixture.Create(0x24, 1003, 0),
                        InstructionFixture.Create(0x0B, 1005),
                    ],
                    1
                ),
            ],
            [host],
            [global]
        );
        (int Frames, int Values, int Depth) state = default;
        void Run()
        {
            var context = InterpreterContext.Enter(new(10), out var outermost);
            try
            {
                try
                {
                    Interpreter.Run(
                        context,
                        (DefinedFunction)instance.Functions[1],
                        [],
                        WasmProcessingStage.Invoke
                    );
                }
                finally
                {
                    state = (context.FrameCount, context.ValueCount, context.CallDepth);
                }
            }
            finally
            {
                InterpreterContext.Exit(outermost);
            }
        }

        // Act & Assert
        if (invalid)
        {
            await Assert.That(Run).ThrowsExactly<InvalidOperationException>();
        }
        else
        {
            await Assert.That(Run).ThrowsNothing();
        }
        using (Assert.Multiple())
        {
            await Assert.That(global.Value.AsI32()).IsEqualTo(invalid ? 0 : 42);
            await Assert.That(state).IsEqualTo((0, 0, 0));
        }
    }
}
