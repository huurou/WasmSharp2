using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;
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
                ConstantInitializer.Validate(
                    expression,
                    WasmValueKind.I32,
                    importedGlobals,
                    [],
                    [],
                    11,
                    WasmSpecVersion.Core2
                )
            )
            .ThrowsNothing();
    }

    [Test]
    public async Task 同じ命令を異なる順序で検証する_型スタックの最大深さが32を超えるとヒープ割り当てが増える()
    {
        // Arrange
        var shallow = CreateAdditionExpression(2);
        var atLimit = CreateAdditionExpression(32);
        var aboveLimit = CreateAdditionExpression(33);
        var deepest = CreateAdditionExpression(129);
        for (var i = 0; i < 16; i++)
        {
            MeasureAllocatedBytes(shallow);
            MeasureAllocatedBytes(atLimit);
            MeasureAllocatedBytes(aboveLimit);
            MeasureAllocatedBytes(deepest);
        }
        var beforeBuffer = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(new WasmValueType[deepest.Length]);
        var oneBufferBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBuffer;

        // Act
        var shallowBytes = MeasureAllocatedBytes(shallow);
        var atLimitBytes = MeasureAllocatedBytes(atLimit);
        var aboveLimitBytes = MeasureAllocatedBytes(aboveLimit);
        var deepestBytes = MeasureAllocatedBytes(deepest);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(atLimitBytes).IsEqualTo(shallowBytes);
            await Assert.That(aboveLimitBytes).IsGreaterThan(atLimitBytes);
            await Assert.That(deepestBytes - shallowBytes).IsLessThanOrEqualTo(oneBufferBytes);
        }
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
            .That(() => ConstantInitializer.Validate(expression, WasmValueKind.I32, [], [], [], 11, WasmSpecVersion.Core2))
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
            .That(() => ConstantInitializer.Validate(expression, WasmValueKind.I32, [], [], [], 11, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("constant expression required: 定数式に使用できない命令");
            await Assert.That(exception.Location).IsEqualTo(new(WasmProcessingStage.Validate, 40, null, 11));
        }
    }

    private static ImmutableArray<DecodedInstruction> CreateAdditionExpression(int maximumDepth)
    {
        const int OperandCount = 129;
        var constant = DecodedInstruction.WithImmediate(new OpcodeKey(0, 0x41), WasmValue.FromI32(1), 0);
        var add = DecodedInstruction.WithoutImmediate(new OpcodeKey(0, 0x6A), 0);
        var expression = ImmutableArray.CreateBuilder<DecodedInstruction>(OperandCount * 2);
        expression.AddRange(Enumerable.Repeat(constant, maximumDepth));
        for (var i = maximumDepth; i < OperandCount; i++)
        {
            expression.Add(add);
            expression.Add(constant);
        }
        expression.AddRange(Enumerable.Repeat(add, maximumDepth - 1));
        expression.Add(DecodedInstruction.WithoutImmediate(new OpcodeKey(0, 0x0B), 0));
        return expression.MoveToImmutable();
    }

    private static long MeasureAllocatedBytes(ImmutableArray<DecodedInstruction> expression)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        ConstantInitializer.Validate(expression, WasmValueKind.I32, [], [], [], 6, WasmSpecVersion.Core3);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
