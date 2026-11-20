using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ConstantInitializer_ValidateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task I32の定数または不変globalを指定する_データセクションの定数式として受理する(bool useGlobal)
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            useGlobal
                ? DecodedInstruction.WithIndex(new(0, 0x23), 1, 40)
                : DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(-1), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];
        WasmGlobalType[] importedGlobals = [new(WasmValueKind.I64, false), new(WasmValueKind.I32, false)];

        // Act & Assert
        await Assert
            .That(() =>
                ConstantInitializer.Validate(expression, WasmValueKind.I32, importedGlobals, 0, 11, WasmSpecVersion.Core2)
            )
            .ThrowsNothing();
    }

    [Test]
    public async Task 定数式の長さが32命令を超える_型検証のヒープ割り当てが増える()
    {
        // Arrange
        var belowLimit = CreateAdditionExpression(30);
        var atLimit = CreateAdditionExpression(32);
        var aboveLimit = CreateAdditionExpression(34);
        for (var i = 0; i < 16; i++)
        {
            MeasureAllocatedBytes(belowLimit);
            MeasureAllocatedBytes(atLimit);
            MeasureAllocatedBytes(aboveLimit);
        }

        // Act
        // どちらも定数32命令、加算30命令、終端2命令を検証する。34命令の式だけが型スタックをヒープに確保する。
        var withinLimit = MeasureAllocatedBytes(atLimit) + MeasureAllocatedBytes(atLimit);
        var crossingLimit = MeasureAllocatedBytes(belowLimit) + MeasureAllocatedBytes(aboveLimit);

        // Assert
        await Assert.That(crossingLimit).IsGreaterThan(withinLimit);
    }

    [Test]
    public async Task I64の式にI32を要求する_指定セクションの終端位置で型不一致を報告する()
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithImmediate(new(0, 0x42), WasmValue.FromI64(0), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];

        // Act & Assert
        var exception = await Assert
            .That(() => ConstantInitializer.Validate(expression, WasmValueKind.I32, [], 0, 11, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch: 定数式の結果型");
            await Assert.That(exception.Location).IsEqualTo(new(WasmProcessingStage.Validate, 42, null, 11));
        }
    }

    [Test]
    [Arguments((byte)0x6A)]
    [Arguments((byte)0x00)]
    public async Task 数値命令や制御命令を指定する_指定セクションの命令位置で定数式違反を報告する(byte opcode)
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithoutImmediate(new(0, opcode), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 41),
        ];

        // Act & Assert
        var exception = await Assert
            .That(() => ConstantInitializer.Validate(expression, WasmValueKind.I32, [], 0, 11, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("constant expression required: 定数式に使用できない命令");
            await Assert.That(exception.Location).IsEqualTo(new(WasmProcessingStage.Validate, 40, null, 11));
        }
    }

    private static ImmutableArray<DecodedInstruction> CreateAdditionExpression(int instructionCount)
    {
        var operandCount = instructionCount / 2;
        var constant = DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(1), 0);
        var add = DecodedInstruction.WithoutImmediate(new(0, 0x6A), 0);
        return
        [
            .. Enumerable.Repeat(constant, operandCount),
            .. Enumerable.Repeat(add, operandCount - 1),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 0),
        ];
    }

    private static long MeasureAllocatedBytes(ImmutableArray<DecodedInstruction> expression)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        ConstantInitializer.Validate(expression, WasmValueKind.I32, [], 0, 6, WasmSpecVersion.Core3);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
