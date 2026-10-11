using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments("02400C00000B41090B", 9)]
    [Arguments("41090C0041000B", 9)]
    [Arguments("4109027F41010C0041000B6A0B", 10)]
    [Arguments("4101047F41070C0042001A0541080B0B", 7)]
    [Arguments("4100047F41070C0042001A0541080B0B", 8)]
    [Arguments("027F410741000D001A41080B0B", 8)]
    [Arguments("027F410741010D001A41080B0B", 7)]
    public async Task 前方分岐または非分岐を実行_保持値を使い飛び越えた命令を実行しない(
        string body,
        int expected
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act
        var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

        // Assert
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(expected);
    }

    [Test]
    public async Task 複数の入れ子を越えて分岐する_外側operandと複数結果の順序を保持する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F, 0x7F, 0x7E]), ([], [0x7F, 0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    ([], Convert.FromHexString("410902010240410A42140C01000B000B0B"))
                )
            )
        );

        // Act
        var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

        // Assert
        await Assert
            .That(
                result.Values.SequenceEqual([
                    WasmValue.FromI32(9),
                    WasmValue.FromI32(10),
                    WasmValue.FromI64(20),
                ])
            )
            .IsTrue();
    }

    [Test]
    public async Task loop引数を持つ後方分岐を多数回実行_値や関数frameや深さを蓄積しない()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7E])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Code(
                        ([], Convert.FromHexString("2000030041016B220020000D001A42070B0B"))
                    )
                )
            )
            .Validate();
        var function = (DefinedFunction)module.Instantiate([], new(1)).Functions[0];
        var context = InterpreterContext.Enter(new(1), out var isOutermost);
        ExecutionResult result;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                function,
                [WasmValue.FromI32(10000)],
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
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values[0].AsI64()).IsEqualTo(7L);
            await Assert.That(state).IsEqualTo((0, 0, 0));
            await Assert.That(module.FunctionCodes[0].MaxOperandStack).IsEqualTo(2);
            await Assert
                .That(module.FunctionCodes[0].BranchTargets[0])
                .IsEqualTo(new BranchTarget(1, 0, 1));
        }
    }

    [Test]
    [Arguments("024042090C01000B000B")]
    [Arguments("024042090F000B000B")]
    public async Task 呼出し先が関数ラベルまたはreturnで終了する_呼出し元へ一度だけ戻る(string body)
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F, 0x7E]), ([], [0x7E])),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Code(
                        ([], Convert.FromHexString("412A10010B")),
                        ([], Convert.FromHexString(body))
                    )
                )
            )
            .Validate();
        var function = (DefinedFunction)module.Instantiate([], new(2)).Functions[0];
        var context = InterpreterContext.Enter(new(2), out var isOutermost);
        ExecutionResult result;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            result = Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
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
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(result.Values[1].AsI64()).IsEqualTo(9L);
            await Assert.That(state).IsEqualTo((0, 0, 0));
        }
    }
}
