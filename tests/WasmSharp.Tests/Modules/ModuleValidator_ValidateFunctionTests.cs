using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments("60017F017F", "0041010B", 0UL, 1)]
    [Arguments("6000017F", "01017F41010B", 1UL, 1)]
    [Arguments("6000017F", "01FFFFFFFF0F7F41010B", 4294967295UL, 1)]
    [Arguments("600000", "000B", 0UL, 0)]
    [Arguments("6000027F7E", "00410142020B", 0UL, 2)]
    public async Task 引数やlocalsや複数結果の型が有効_圧縮localsと最大operand数を保持する(
        string signature,
        string body,
        ulong localCount,
        int maxOperandStack
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(codes.Length).IsEqualTo(1);
            await Assert.That(codes[0].LocalCount).IsEqualTo(localCount);
            await Assert.That(codes[0].Locals.Length).IsEqualTo(module.Functions[0].Locals.Length);
            await Assert.That(codes[0].MaxOperandStack).IsEqualTo(maxOperandStack);
        }
    }

    [Test]
    [Arguments("600000", "001A0B", 0)]
    [Arguments("60017F017F", "0020010B", 0)]
    [Arguments("60017F017F", "0020FFFFFFFF0F0B", 0)]
    [Arguments("60017F017F", "004201210041000B", 1)]
    [Arguments("60017F017F", "00420122000B", 1)]
    [Arguments("60017F017F", "00210041000B", 0)]
    [Arguments("60017F017F", "0022000B", 0)]
    [Arguments("60017F017F", "00410021010B", 1)]
    [Arguments("60017F017F", "00410022FFFFFFFF0F0B", 1)]
    [Arguments("60017F017F", "0010000B", 0)]
    [Arguments("60017F017F", "00420110000B", 1)]
    [Arguments("6000017F", "0010010B", 0)]
    [Arguments("6000017F", "0010FFFFFFFF0F0B", 0)]
    [Arguments("60027F7E017F", "004101410210000B", 2)]
    [Arguments("6000017F", "000F0B", 0)]
    [Arguments("6000017F", "0042010F0B", 1)]
    [Arguments("6000027F7E", "0041010F0B", 1)]
    [Arguments("6000027F7E", "00420141010F0B", 2)]
    public async Task 命令の添字や入力型や個数が不正_命令位置付き検証失敗になる(
        string signature,
        string body,
        int instructionIndex
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);
        var offset = module.Functions[0].Instructions[instructionIndex].ByteOffset;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, offset, 0, 10));
        await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments("23020B", 0)]
    [Arguments("23FFFFFFFF0F0B", 0)]
    [Arguments("410024020B", 1)]
    [Arguments("410024FFFFFFFF0F0B", 1)]
    [Arguments("410024000B", 1)]
    [Arguments("4200240141000B", 1)]
    [Arguments("240141000B", 0)]
    public async Task Globalの添字や可変性や入力型が不正_元関数と命令位置付き検証失敗になる(
        string body,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0]), ("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B])),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Functions[0].Instructions[instructionIndex].ByteOffset,
                    1,
                    10
                )
            );
    }

    [Test]
    public async Task 型の異なるimportと定義へcallする_種類別添字で入力と複数結果を検証する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(
                    ([0x7F, 0x7E], [0x7E, 0x7F]),
                    ([], [0x7E, 0x7F]),
                    ([], [])
                ),
                HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0]), ("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(1, 2),
                HostLinkingModuleBinary.Code(
                    ([], [0x10, 2, 0x41, 42, 0x42, 7, 0x10, 0, 0x0B]),
                    ([], [0x0B])
                )
            )
        );

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(codes[0].MaxOperandStack).IsEqualTo(2);
            await Assert.That(codes[0].Instructions[0].Opcode).IsEqualTo(ExecutionOpcode.Op10);
            await Assert.That(codes[0].Instructions[0].Index).IsEqualTo(2u);
            await Assert.That(codes[0].Instructions[3].Index).IsEqualTo(0u);
            await Assert
                .That(codes[0].Instructions[3].ByteOffset)
                .IsEqualTo(module.Functions[0].Instructions[3].ByteOffset);
        }
    }

    [Test]
    public async Task Global取得だけで結果を積む_最大operand数に反映する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F])),
                HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0x23, 0, 0x0B]))
            )
        );

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes[0].MaxOperandStack).IsEqualTo(1);
    }

    [Test]
    public async Task Callの結果が引数より多い_最大operand数に反映する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7E, 0x7F])),
                HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]))
            )
        );

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes[0].MaxOperandStack).IsEqualTo(2);
    }

    [Test]
    public async Task 圧縮localsの前後と途中に個数0の宣言がある_引数と各宣言の境界の型を解決する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7F, 0x7E, 0x7E, 0x7D])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (
                        [(0, 0x7C), (2, 0x7E), (0, 0x7D), (0, 0x7C), (1, 0x7D), (0, 0x7C)],
                        [0x20, 0, 0x20, 1, 0x20, 2, 0x20, 3, 0x0B]
                    )
                )
            )
        );

        // Act
        var result = module.Validate();

        // Assert
        await Assert.That(result).IsSameReferenceAs(module);
        await Assert.That(module.FunctionCodes[0].LocalCount).IsEqualTo(3UL);
    }

    [Test]
    public async Task 圧縮localsの末尾の個数0宣言を参照する_命令位置で範囲外を拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([(2, 0x7E), (0, 0x7F), (0, 0x7D)], [0x20, 3, 0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Functions[0].Instructions[0].ByteOffset,
                    0,
                    10
                )
            );
    }

    [Test]
    public async Task 圧縮localsの宣言が多数ある_末尾の型を解決する()
    {
        // Arrange
        (uint Count, byte Kind)[] locals =
        [
            .. Enumerable.Repeat((1u, (byte)0x7F), 1023),
            (1u, 0x7E),
        ];
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(
                    (locals, [0x20, .. HostLinkingModuleBinary.Unsigned(1023), 0x0B])
                )
            )
        );

        // Act
        var result = module.Validate();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).IsSameReferenceAs(module);
            await Assert.That(module.FunctionCodes[0].LocalCount).IsEqualTo(1024UL);
        }
    }

    [Test]
    public async Task 圧縮localsが巨大で末尾を参照する_型検証は成功し実行時の保持上限で拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    (
                        [(0, 0x7D), (uint.MaxValue - 1, 0x7F), (1, 0x7E)],
                        [0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x0B]
                    )
                )
            )
        );
        var function = module.Validate().Instantiate([]).GetFunction("run");

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke([WasmValue.FromI32(1)]))
            .ThrowsExactly<WasmImplementationLimitException>();
        await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
        await Assert.That(module.FunctionCodes[0].LocalCount).IsEqualTo((ulong)uint.MaxValue);
        await Assert.That(module.FunctionCodes[0].Locals.Length).IsEqualTo(3);
    }

    [Test]
    public async Task 後半関数のlocalが不正_再検証でも実行コードとexport索引を公開しない()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7F])),
                HostLinkingModuleBinary.Functions(0, 0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]), ([], [0x20, 1, 0x0B]))
            )
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert.That(exception!.Location!.FunctionIndex).IsEqualTo(1u);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
