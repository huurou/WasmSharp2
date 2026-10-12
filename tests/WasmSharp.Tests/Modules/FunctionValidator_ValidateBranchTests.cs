using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    [Arguments("02400C00000B0B", "", 0, 2, 0, 0)]
    [Arguments("4109027F41010C00000B6A0B", "7F", 2, 4, 1, 1)]
    [Arguments("41090C00000B", "7F", 1, 3, 0, 1)]
    [Arguments("02400240410142020C02000B000B000B", "7F7E", 2, 6, 0, 2)]
    [Arguments("4101420241000D000B", "7F7E", 3, 4, 0, 2)]
    public async Task 単一ラベルへ分岐する_制御終了または最終Returnへの確定済み情報を出力する(
        string body,
        string results,
        int branchPc,
        int targetPc,
        int height,
        int keepCount
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets.Length).IsEqualTo(1);
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[branchPc].Index])
                .IsEqualTo(new BranchTarget(targetPc, height, keepCount));
            await Assert.That(code.Instructions[^1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
            await Assert
                .That(code.Instructions.Count(x => x.Opcode == ExecutionOpcode.Op0B))
                .IsEqualTo(1);
            await Assert
                .That(
                    code.BranchTargets.All(x =>
                        x.TargetPc >= 0 && x.TargetPc < code.Instructions.Length
                    )
                )
                .IsTrue();
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task If両側が同じラベルへ分岐する_条件入口と各前方分岐を別々に確定する()
    {
        // Arrange
        var module = DecodeControl(
            [],
            [0x7F],
            Convert.FromHexString("4101047F41010C00000541020C00000B0B")
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets.Length).IsEqualTo(4);
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[1].Index])
                .IsEqualTo(new BranchTarget(6, 0, 0));
            foreach (var branchPc in new[] { 3, 5, 7 })
            {
                await Assert
                    .That(code.BranchTargets[(int)code.Instructions[branchPc].Index])
                    .IsEqualTo(new BranchTarget(9, 0, 1));
            }
        }
    }

    [Test]
    [Arguments(0x02, 0x42, 5)]
    [Arguments(0x03, 0x41, 1)]
    public async Task 制御の引数と結果の型が異なる_blockは結果と前方終了でloopは引数と後方入口へ分岐する(
        int opcode,
        int constantOpcode,
        int targetPc
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7E]), ([0x7F], [0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            0x41,
                            9,
                            (byte)opcode,
                            1,
                            0x1A,
                            (byte)constantOpcode,
                            1,
                            0x0C,
                            0,
                            0,
                            0x0B,
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
            await Assert.That(code.BranchTargets[0]).IsEqualTo(new BranchTarget(targetPc, 0, 1));
            await Assert.That(code.Instructions[3].Opcode).IsEqualTo(ExecutionOpcode.Op0C);
            await Assert.That(code.MaxOperandStack).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(0x02, 0x41)]
    [Arguments(0x03, 0x42)]
    public async Task 分岐値がblock結果またはloop引数と不一致_他方の宣言型で補わず拒否する(
        int opcode,
        int constantOpcode
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7E]), ([0x7F], [0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            0x41,
                            9,
                            (byte)opcode,
                            1,
                            0x1A,
                            (byte)constantOpcode,
                            1,
                            0x0C,
                            0,
                            0,
                            0x0B,
                            0x0B,
                        ]
                    )
                )
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location!.ByteOffset)
                .IsEqualTo(module.Functions[0].Instructions[4].ByteOffset);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments("0C001A0B", "")]
    [Arguments("027F000C000B0B", "7F")]
    [Arguments("000D0042011A0B", "7F")]
    public async Task 分岐後の現在frame底が多相的_有効なUnknownを受け付ける(
        string body,
        string results
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        await Assert.That(code.BranchTargets.Length).IsEqualTo(1);
    }

    [Test]
    [Arguments("0C010B", "", 0, "unknown label 1")]
    [Arguments("0D010B", "", 0, "unknown label 1")]
    [Arguments("42000D010B", "", 1, "unknown label 1")]
    [Arguments("000CFFFFFFFF0F0B", "", 1, "unknown label 4294967295")]
    [Arguments("02400C020B0B", "", 1, "unknown label 2")]
    [Arguments("027F0C000B1A0B", "", 1, "type mismatch")]
    [Arguments("027F42000C000B1A0B", "", 2, "type mismatch")]
    [Arguments("027F41000D000B1A0B", "", 2, "type mismatch")]
    [Arguments("027F410142000D000B1A0B", "", 3, "type mismatch")]
    [Arguments("0C0042016A0B", "", 2, "type mismatch")]
    [Arguments("000D0042017C0B", "7F", 3, "type mismatch")]
    [Arguments("0C00024041010B0B", "", 3, "type mismatch")]
    [Arguments("0C00027F0B1A0B", "", 2, "type mismatch")]
    [Arguments("02400C000B1A0B", "", 3, "type mismatch")]
    [Arguments("02400F0B1A0B", "", 3, "type mismatch")]
    public async Task 分岐ラベルや局所的型規則が不正_ラベル先行の診断と元の位置を保つ(
        string body,
        string results,
        int failureIndex,
        string prefix
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

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
