using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class ExecutionBoundary_RunStartTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ホストstartを実行する_所有instanceの上限で一段だけ数えて終了時に解除する(
        bool withInstance
    )
    {
        // Arrange
        var instance = CreateInstance(1);
        WasmInstance? received = null;
        (int Limit, int Depth, int Frames, WasmProcessingStage Stage) state = default;
        var calls = 0;
        WasmResults Callback()
        {
            var context = InterpreterContext.Current!;
            state = (context.MaxCallDepth, context.CallDepth, context.FrameCount, context.Stage);
            calls++;
            return new([]);
        }
        var function = withInstance
            ? WasmFunction.CreateHost(
                new([], []),
                (current, _) =>
                {
                    received = current;
                    return Callback();
                }
            )
            : WasmFunction.CreateHost(new([], []), _ => Callback());

        // Act
        ExecutionBoundary.RunStart(instance, function);
        var firstCleared = InterpreterContext.Current is null;
        ExecutionBoundary.RunStart(instance, function);
        var secondCleared = InterpreterContext.Current is null;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(state).IsEqualTo((1, 1, 0, WasmProcessingStage.Instantiate));
            await Assert.That(ReferenceEquals(received, withInstance ? instance : null)).IsTrue();
            await Assert.That(firstCleared && secondCleared).IsTrue();
        }
    }

    [Test]
    public async Task Import定義startを実行する_所有instanceの上限と定義元のglobalを使う()
    {
        // Arrange
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x00, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("value", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x10, 0x01, 0x0B]),
                        ([], [0x41, 0x07, 0x24, 0x00, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([], new(1));
        var owner = CreateInstance(2);

        // Act
        ExecutionBoundary.RunStart(owner, source.GetFunction("run"));
        var cleared = InterpreterContext.Current is null;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(source.GetGlobal("value").AsI32()).IsEqualTo(7);
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    public async Task 定義startでtrapになる_Instantiate段階と元位置を示して解除する()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [InstructionFixture.Create(0x00, 77)],
            new([], []),
            [],
            0
        );
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    ExecutionBoundary.RunStart(function.Instance, function);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Instantiate, 77, 1));
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    public async Task 定義startが再帰で上限に達する_所有instanceの上限とInstantiate段階を示す()
    {
        // Arrange
        var function = ExecutionFunctionFixture.Create(
            [InstructionFixture.Create(0x10, 77, 1)],
            new([], []),
            [],
            0
        );
        var owner = CreateInstance(3);
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    ExecutionBoundary.RunStart(owner, function);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(3);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(
                        WasmProcessingStage.Instantiate,
                        function.Definition.BodyOffset,
                        1
                    )
                );
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 既存contextの上限でstartを拒否する_外側の値と深さと段階を復元する(bool host)
    {
        // Arrange
        var owner = CreateInstance(100);
        var called = false;
        var function = host
            ? WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    called = true;
                    return new([]);
                }
            )
            : ExecutionFunctionFixture.Create(
                [InstructionFixture.Create(0x0B, 77)],
                new([], []),
                [],
                0
            );
        var sameContext = false;
        (int Frames, int Values, int Depth, WasmProcessingStage Stage, int Value) state = default;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var outer = InterpreterContext.Enter(new(1), out var isOutermost);
                try
                {
                    outer.Stage = WasmProcessingStage.Invoke;
                    outer.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
                    outer.PushValue(WasmValue.FromI32(42));
                    outer.TryEnterCall();
                    try
                    {
                        ExecutionBoundary.RunStart(owner, function);
                    }
                    finally
                    {
                        sameContext = ReferenceEquals(outer, InterpreterContext.Current);
                        state = (
                            outer.FrameCount,
                            outer.ValueCount,
                            outer.CallDepth,
                            outer.Stage,
                            outer.GetValue(0).AsI32()
                        );
                    }
                }
                finally
                {
                    outer.Restore(0, 0, 0);
                    InterpreterContext.Exit(isOutermost);
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Limit).IsEqualTo(1);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
            await Assert.That(sameContext).IsTrue();
            await Assert.That(state).IsEqualTo((0, 1, 1, WasmProcessingStage.Invoke, 42));
            await Assert.That(called).IsFalse();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ホストstartからの再入が失敗する_Invoke段階の例外実体を変更せず解除する(
        bool exhaustion
    )
    {
        // Arrange
        var owner = CreateInstance(3);
        var inner = ExecutionFunctionFixture.Create(
            [InstructionFixture.Create(exhaustion ? 0x10U : 0x00U, 77, 1)],
            new([], []),
            [],
            0
        );
        Exception? original = null;
        var cleared = false;
        var host = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                try
                {
                    return inner.Invoke([]);
                }
                catch (Exception exception)
                {
                    original = exception;
                    throw;
                }
            }
        );

        // Act & Assert
        var actual = await Assert
            .That(() =>
            {
                try
                {
                    ExecutionBoundary.RunStart(owner, host);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .Throws<Exception>();
        var location = actual switch
        {
            WasmTrapException trap => trap.Location,
            WasmExhaustionException exhausted => exhausted.Location,
            _ => null,
        };
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(actual, original)).IsTrue();
            await Assert.That(location?.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(cleared).IsTrue();
        }
    }

    private static WasmInstance CreateInstance(int maxCallDepth)
    {
        return WasmModule
            .Decode(HostLinkingModuleBinary.Create())
            .Validate()
            .Instantiate([], new(maxCallDepth));
    }
}
