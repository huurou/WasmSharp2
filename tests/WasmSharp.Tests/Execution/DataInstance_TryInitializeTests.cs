using WasmSharp.Execution;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Execution;

internal class DataInstance_TryInitializeTests
{
    [Test]
    public async Task コピー元とコピー先が範囲内にある_指定範囲だけ転送する()
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        var memory = new WasmMemory(new WasmLimits(1));
        memory.Write(0, [99, 99, 99, 99, 99, 99]);
        var actual = new byte[6];

        // Act
        var success = data.TryInitialize(memory, 2, 1, 2);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 99, 99, 20, 30, 99, 99 })).IsTrue();
            await Assert.That(data.Length).IsEqualTo(4u);
        }
    }

    [Test]
    public async Task コピー先がページ境界をまたぐ_全範囲を転送する()
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        var memory = new WasmMemory(new WasmLimits(2));
        var actual = new byte[4];

        // Act
        var success = data.TryInitialize(memory, 65534, 0, 4);
        memory.Read(65534, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 10, 20, 30, 40 })).IsTrue();
        }
    }

    [Test]
    public async Task 転送後にコピー先を書き換えて再転送する_元のデータを保持している()
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        var memory = new WasmMemory(new WasmLimits(1));
        var actual = new byte[4];

        // Act
        var firstSuccess = data.TryInitialize(memory, 0, 0, 4);
        memory.Write(0, [99, 99, 99, 99]);
        var secondSuccess = data.TryInitialize(memory, 8, 0, 4);
        memory.Read(8, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstSuccess).IsTrue();
            await Assert.That(secondSuccess).IsTrue();
            await Assert.That(data.Length).IsEqualTo(4u);
            await Assert.That(actual.SequenceEqual(new byte[] { 10, 20, 30, 40 })).IsTrue();
        }
    }

    [Test]
    [Arguments(0u, 0u, 0u)]
    [Arguments(1u, 65536u, 0u)]
    [Arguments(1u, 0u, 4u)]
    [Arguments(1u, 65536u, 4u)]
    public async Task 長さがゼロで両開始位置が領域内または末尾にある_成功する(
        uint pages,
        uint destination,
        uint source
    )
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        var memory = new WasmMemory(new WasmLimits(pages));

        // Act
        var success = data.TryInitialize(memory, destination, source, 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(0u, 3u, 2u)]
    [Arguments(0u, 5u, 0u)]
    [Arguments(65535u, 0u, 2u)]
    [Arguments(65537u, 0u, 0u)]
    [Arguments(0u, uint.MaxValue, 2u)]
    [Arguments(0u, 1u, uint.MaxValue)]
    [Arguments(0u, 0u, 0x80000000u)]
    [Arguments(0u, 0x80000000u, 0u)]
    [Arguments(uint.MaxValue, 0u, 2u)]
    public async Task いずれかの範囲が領域外にある_内容を一部も変更せずfalseを返す(
        uint destination,
        uint source,
        uint length
    )
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        var memory = new WasmMemory(new WasmLimits(1));
        var expected = new byte[65536];
        Array.Fill(expected, (byte)42);
        expected[0] = 1;
        expected[1] = 2;
        expected[^1] = 3;
        memory.Write(0, expected);
        var actual = new byte[expected.Length];

        // Act
        var success = data.TryInitialize(memory, destination, source, length);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
            await Assert.That(data.Length).IsEqualTo(4u);
        }
    }

    [Test]
    [Arguments(0u, 0u, 0u, true)]
    [Arguments(65536u, 0u, 0u, true)]
    [Arguments(65537u, 0u, 0u, false)]
    [Arguments(0u, 1u, 0u, false)]
    [Arguments(0u, 0u, 1u, false)]
    public async Task 破棄済みである_コピー元位置と長さがゼロかつコピー先が範囲内の場合だけ成功する(
        uint destination,
        uint source,
        uint length,
        bool expectedSuccess
    )
    {
        // Arrange
        var data = new DataInstance(new DataDefinition([10, 20, 30, 40], null, 0));
        data.Drop();
        var memory = new WasmMemory(new WasmLimits(1));
        var expected = new byte[65536];
        Array.Fill(expected, (byte)42);
        memory.Write(0, expected);
        var actual = new byte[expected.Length];

        // Act
        var success = data.TryInitialize(memory, destination, source, length);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsEqualTo(expectedSuccess);
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        }
    }
}
