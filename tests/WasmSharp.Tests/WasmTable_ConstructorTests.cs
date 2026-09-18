using WasmSharp.Exceptions;

namespace WasmSharp.Tests;

internal class WasmTable_ConstructorTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, null)]
    [Arguments(WasmValueKind.ExternRef, 3u)]
    [Arguments(WasmValueKind.FuncRef, uint.MaxValue)]
    public async Task 参照型とlimitsを指定する_型別nullで初期化して現在数と最大値を保持する(
        WasmValueKind kind,
        uint? maximum
    )
    {
        // Arrange
        var limits = new WasmLimits(2, maximum);

        // Act
        var table = new WasmTable(kind, limits);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(table.ElementType).IsEqualTo(kind);
            await Assert.That(table.Count).IsEqualTo(2u);
            await Assert.That(table.MaximumElements).IsEqualTo(maximum);
            for (uint index = 0; index < 2; index++)
            {
                var value = table.Get(index);
                await Assert.That(value.Kind).IsEqualTo(kind);
                await Assert
                    .That(kind == WasmValueKind.FuncRef ? value.AsFuncRef() : value.AsExternRef())
                    .IsNull();
            }
        }
    }

    [Test]
    public async Task 最小最大ゼロを指定する_空のtableを生成する()
    {
        // Act
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(0, 0));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(table.Count).IsEqualTo(0u);
            await Assert.That(table.MaximumElements).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32)]
    [Arguments(WasmValueKind.V128)]
    [Arguments(WasmValueKind.ExnRef)]
    [Arguments((WasmValueKind)255)]
    public async Task 要素型が対応する参照型ではない_契約違反として拒否する(WasmValueKind kind)
    {
        // Act & Assert
        await Assert.That(() => new WasmTable(kind, new WasmLimits(0))).Throws<ArgumentException>();
    }

    [Test]
    public async Task 最小値が最大値を超える_契約違反として拒否する()
    {
        // Act & Assert
        await Assert
            .That(() => new WasmTable(WasmValueKind.FuncRef, new WasmLimits(2, 1)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Limitsがnullである_引数例外として拒否する()
    {
        // Act & Assert
        await Assert
            .That(() => new WasmTable(WasmValueKind.FuncRef, null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task 初期要素数が配列保持上限を超える_割当前に実装上限として拒否する()
    {
        // Arrange
        var limits = new WasmLimits((uint)Array.MaxLength + 1);

        // Act & Assert
        var exception = await Assert
            .That(() => new WasmTable(WasmValueKind.FuncRef, limits))
            .ThrowsExactly<WasmImplementationLimitException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
            await Assert.That(exception.Location).IsNull();
        }
    }
}
