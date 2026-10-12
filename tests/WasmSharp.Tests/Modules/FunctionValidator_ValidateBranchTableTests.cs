using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    [Arguments("024041000E00001A0B0B", "", 1, 0, 3, 3, 0, 0)]
    [Arguments("4109027F410741000E010001000B6A0B", "7F", 3, 1, 5, 6, 1, 1)]
    public async Task 分岐表が空候補または単一候補を持つ_候補の後に既定を置き確定した高さと保持数を保存する(
        string body,
        string results,
        int branchPc,
        int candidateCount,
        int candidatePc,
        int defaultPc,
        int candidateHeight,
        int keepCount
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        var instruction = code.Instructions[branchPc];
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Opcode).IsEqualTo(ExecutionOpcode.Op0E);
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.TargetCount).IsEqualTo(candidateCount);
            await Assert.That(code.BranchTargets.Length).IsEqualTo(candidateCount + 1);
            if (candidateCount != 0)
            {
                await Assert
                    .That(code.BranchTargets[0])
                    .IsEqualTo(new BranchTarget(candidatePc, candidateHeight, keepCount));
            }
            await Assert
                .That(code.BranchTargets[candidateCount])
                .IsEqualTo(new BranchTarget(defaultPc, 0, keepCount));
            await Assert.That(code.Instructions[^1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task 分岐表がloopとblockと関数へ複数値を渡す_先行分岐に続けて入力順の候補と既定を確定する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F, 0x7E]), ([0x7F, 0x7E], [0x7F, 0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        Convert.FromHexString(
                            "41014202030141000D0002004103420441000E02010002000B1A1A0B0B"
                        )
                    )
                )
            )
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        var instruction = code.Instructions[7];
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Opcode).IsEqualTo(ExecutionOpcode.Op0E);
            await Assert.That(instruction.Index).IsEqualTo(1u);
            await Assert.That(instruction.TargetCount).IsEqualTo(2);
            await Assert.That(code.BranchTargets.Length).IsEqualTo(4);
            await Assert.That(code.BranchTargets[0]).IsEqualTo(new BranchTarget(2, 0, 2));
            await Assert.That(code.BranchTargets[1]).IsEqualTo(new BranchTarget(2, 0, 2));
            await Assert.That(code.BranchTargets[2]).IsEqualTo(new BranchTarget(9, 2, 2));
            await Assert.That(code.BranchTargets[3]).IsEqualTo(new BranchTarget(11, 0, 2));
            await Assert.That(code.MaxOperandStack).IsEqualTo(5);
            await Assert
                .That(
                    code.BranchTargets.All(x =>
                        x.TargetPc >= 0 && x.TargetPc < code.Instructions.Length
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("7C", "7D")]
    [Arguments("7E7C", "7F7D")]
    public async Task 到達不能の分岐値が全てUnknown_具体型の異なるラベルへ同じ列を照合して許可する(
        string outerResults,
        string innerResults
    )
    {
        // Arrange
        var outerKinds = Convert.FromHexString(outerResults);
        var innerKinds = Convert.FromHexString(innerResults);
        byte[] drops = [.. Enumerable.Repeat((byte)0x1A, innerKinds.Length)];
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], []), ([], outerKinds), ([], innerKinds)),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            0x02,
                            1,
                            0x02,
                            2,
                            0,
                            0x41,
                            1,
                            0x0E,
                            2,
                            0,
                            1,
                            1,
                            0x0B,
                            .. drops,
                            0,
                            0x0B,
                            .. drops,
                            0x0B,
                        ]
                    )
                )
            )
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets.Length).IsEqualTo(3);
            await Assert
                .That(code.BranchTargets.All(x => x.KeepCount == innerKinds.Length))
                .IsTrue();
            await Assert.That(code.BranchTargets[0].TargetPc).IsEqualTo(3);
            await Assert.That(code.BranchTargets[1]).IsEqualTo(code.BranchTargets[2]);
        }
    }

    [Test]
    [Arguments("027C027D0041010E020001010B1A4400000000000000000B1A0B")]
    [Arguments("027C027D000E020001010B1A4400000000000000000B1A0B")]
    public async Task Meet_bottomの分岐値とselectorが多相的_内側f32と外側f64の型を一律一致させない(
        string body
    )
    {
        // Arrange
        var module = DecodeControl([], [], Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets.Length).IsEqualTo(3);
            await Assert.That(code.BranchTargets[0].KeepCount).IsEqualTo(1);
            await Assert.That(code.BranchTargets[1]).IsEqualTo(code.BranchTargets[2]);
        }
    }

    [Test]
    [Arguments("42000E0101020B", 1, "unknown label 2")]
    [Arguments("027C430000000042000E0102000B1A0B", 3, "type mismatch")]
    [Arguments("027C0E0102000B1A0B", 1, "unknown label 2")]
    [Arguments("027D430000000042000E0102000B1A0B", 3, "unknown label 2")]
    [Arguments("0E02FFFFFFFF0F01000B", 0, "unknown label 4294967295")]
    [Arguments("027C027D430000000041000E020103000B1A4400000000000000000B1A0B", 4, "type mismatch")]
    [Arguments(
        "027C027D430000000041000E020301000B1A4400000000000000000B1A0B",
        4,
        "unknown label 3"
    )]
    [Arguments("027C027D00430000000041000E0100010B1A4400000000000000000B1A0B", 5, "type mismatch")]
    [Arguments("027F000E0101000B1A0B", 2, "type mismatch")]
    [Arguments("027F000E0100010B1A0B", 2, "type mismatch")]
    [Arguments("027D430000000042000E0100000B1A0B", 3, "type mismatch")]
    [Arguments("0E00000B", 0, "type mismatch")]
    [Arguments("027F41000E00000B1A0B", 2, "type mismatch")]
    [Arguments("4300000000027C41000E0102000B1A1A0B", 3, "unknown label 2")]
    [Arguments("410102400E00000B1A0B", 2, "type mismatch")]
    [Arguments("024041000E00000B1A0B", 4, "type mismatch")]
    [Arguments("41000E000042000B", 3, "type mismatch")]
    public async Task 分岐表のラベルや型が複合的に不正_既定と候補順を優先して元の命令位置で拒否する(
        string body,
        int failureIndex,
        string prefix
    )
    {
        // Arrange
        var module = DecodeControl([], [], Convert.FromHexString(body));

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[failureIndex].ByteOffset,
                        0,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
