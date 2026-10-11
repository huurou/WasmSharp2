using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    [Arguments("0240010B0340010B0B", "", 3, 0)]
    [Arguments("027F41010B037F41020B6A0B", "7F", 4, 2)]
    [Arguments("0200410142020B0300410342040B1A1A0B", "7F7E", 7, 4)]
    public async Task blockとloopの通常終了が有効_markerを除き関数終端だけReturnを出力する(
        string body,
        string results,
        int outputCount,
        int maximum
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions.Length).IsEqualTo(outputCount);
            await Assert.That(code.Instructions[^1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
            await Assert
                .That(code.Instructions.Count(x => x.Opcode == ExecutionOpcode.Op0B))
                .IsEqualTo(1);
            await Assert.That(code.MaxOperandStack).IsEqualTo(maximum);
            await Assert.That(code.BranchTargets.IsEmpty).IsTrue();
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments(0x02, "")]
    [Arguments(0x03, "")]
    [Arguments(0x02, "7F")]
    [Arguments(0x03, "7F")]
    [Arguments(0x02, "7F7E7D7C7B706F")]
    [Arguments(0x03, "7F7E7D7C7B706F")]
    public async Task 型添字の入口引数と結果が同じ_全値型を順序を保って受け渡す(
        int opcode,
        string kindHex
    )
    {
        // Arrange
        var kinds = Convert.FromHexString(kindHex);
        List<byte> body = [];
        for (byte index = 0; index < kinds.Length; index++)
        {
            body.AddRange([0x20, index]);
        }
        body.AddRange([(byte)opcode, 0, 0x0B, 0x0B]);
        var module = DecodeControl(kinds, kinds, [.. body]);

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.MaxOperandStack).IsEqualTo(kinds.Length);
            await Assert.That(code.Instructions.Length).IsEqualTo(kinds.Length + 1);
            await Assert.That(code.BranchTargets.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task ifとelseが複数引数結果を持つ_前方分岐と両側の最大operandを確定する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(
                    ([0x7F, 0x7E, 0x7F], [0x7F, 0x7E]),
                    ([0x7F, 0x7E], [0x7F, 0x7E])
                ),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [(20, 0x7F)],
                        Convert.FromHexString(
                            "2000200120020401410141021A1A054101410241031A1A1A0B0B"
                        )
                    )
                )
            )
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.MaxOperandStack).IsEqualTo(5);
            await Assert.That(code.LocalCount).IsEqualTo(20UL);
            await Assert.That(code.Instructions.Length).IsEqualTo(16);
            await Assert.That(code.Instructions[3].Opcode).IsEqualTo(ExecutionOpcode.Op04);
            await Assert.That(code.Instructions[8].Opcode).IsEqualTo(ExecutionOpcode.Op05);
            await Assert.That(code.BranchTargets.Length).IsEqualTo(2);
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[3].Index])
                .IsEqualTo(new BranchTarget(9, 0, 2));
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[8].Index])
                .IsEqualTo(new BranchTarget(15, 0, 2));
            await Assert
                .That(code.Instructions[^1].ByteOffset)
                .IsEqualTo(module.Functions[0].Instructions[^1].ByteOffset);
        }
    }

    [Test]
    public async Task 非0のoperand底にifが入れ子になる_各分岐を対応するelseとendへ接続する()
    {
        // Arrange
        var module = DecodeControl(
            [],
            [0x7F, 0x7E],
            Convert.FromHexString("41094101047E4100047E42010542020B0542030B0B")
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions.Length).IsEqualTo(11);
            await Assert.That(code.MaxOperandStack).IsEqualTo(2);
            await Assert.That(code.BranchTargets.Length).IsEqualTo(4);
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[2].Index])
                .IsEqualTo(new BranchTarget(9, 1, 0));
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[4].Index])
                .IsEqualTo(new BranchTarget(7, 1, 0));
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[6].Index])
                .IsEqualTo(new BranchTarget(8, 1, 1));
            await Assert
                .That(code.BranchTargets[(int)code.Instructions[8].Index])
                .IsEqualTo(new BranchTarget(10, 1, 1));
        }
    }

    [Test]
    public async Task 多数のblockが入れ子になる_再帰せずに内側markerを除去する()
    {
        // Arrange
        const int DEPTH = 4096;
        List<byte> body = [];
        for (var index = 0; index < DEPTH; index++)
        {
            body.AddRange([0x02, 0x40]);
        }
        body.Add(0x01);
        body.AddRange(Enumerable.Repeat((byte)0x0B, DEPTH + 1));
        var module = DecodeControl([], [], [.. body]);

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions.Length).IsEqualTo(2);
            await Assert.That(code.Instructions[0].Opcode).IsEqualTo(ExecutionOpcode.Op01);
            await Assert.That(code.Instructions[1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
            await Assert.That(code.MaxOperandStack).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("4101410004000B0B", 3, 1)]
    [Arguments("4101410104000B0B", 3, 1)]
    public async Task else省略の引数型列と結果型列が同じ_if後へ入口値を保持する分岐を確定する(
        string body,
        int targetPc,
        int maximum
    )
    {
        // Arrange
        var module = DecodeControl([0x7F], [0x7F], Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets[0]).IsEqualTo(new BranchTarget(targetPc, 0, 1));
            await Assert.That(code.MaxOperandStack).IsEqualTo(maximum + 1);
            await Assert.That(code.Instructions[^1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
        }
    }

    [Test]
    [Arguments("0240001A0B0B", "", 0)]
    [Arguments("027F000B0B", "7F", 1)]
    [Arguments("004100047F41010F0541020B0B", "7F", 1)]
    [Arguments("41090240000B0B", "7F", 1)]
    public async Task 局所的到達不能が型規則に適合_底のUnknownを許し外側値を保持する(
        string body,
        string results,
        int maximum
    )
    {
        // Arrange
        var module = DecodeControl([], Convert.FromHexString(results), Convert.FromHexString(body));

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        await Assert.That(code.MaxOperandStack).IsEqualTo(maximum);
    }

    [Test]
    [Arguments("024041010B0B", "", 2, "type mismatch")]
    [Arguments("027F0B1A0B", "", 1, "type mismatch")]
    [Arguments("410102401A0B0B", "", 2, "type mismatch")]
    [Arguments("0240000B1A0B", "", 3, "type mismatch")]
    [Arguments("00027F0B1A0B", "", 2, "type mismatch")]
    [Arguments("027F0042010B1A0B", "", 3, "type mismatch")]
    [Arguments("02400041010B0B", "", 3, "type mismatch")]
    [Arguments("02400020000B0B", "", 2, "unknown local 0")]
    [Arguments("420004400B0B", "", 1, "type mismatch")]
    [Arguments("4100047F41010B1A0B", "", 3, "type mismatch")]
    [Arguments("4100047F41010542010B1A0B", "", 5, "type mismatch")]
    [Arguments("4101047F42010541010B1A0B", "", 3, "type mismatch")]
    [Arguments("02010B0B", "", 0, "unknown type 1")]
    [Arguments("02FFFFFFFF0F0B0B", "", 0, "unknown type 4294967295")]
    public async Task 制御の型や個数や添字が不正_元の命令位置で拒否しmoduleを未検証に保つ(
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

    [Test]
    [Arguments("4201410202010B0B", "7F7E", "", 2)]
    [Arguments("410102011A0B0B", "7F", "7E", 3)]
    [Arguments("4101410004011A42010B1A0B", "7F", "7E", 5)]
    [Arguments("0002010B0B", "7F", "", 2)]
    public async Task 型添字の入力順序や結果や省略elseが不適合_宣言に対応する命令位置で拒否する(
        string body,
        string parameters,
        string results,
        int failureIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(
                    ([], []),
                    (Convert.FromHexString(parameters), Convert.FromHexString(results))
                ),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
            .IsTrue();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Functions[0].Instructions[failureIndex].ByteOffset,
                    0,
                    10
                )
            );
    }

    private static WasmModule DecodeControl(byte[] parameters, byte[] results, byte[] body)
    {
        return WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types((parameters, results)),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], body))
            )
        );
    }
}
