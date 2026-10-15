using WasmSharp.Execution;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Execution;

internal class DataInstance_DropTests
{
    [Test]
    public async Task 同じ定義から生成した片方を破棄する_他方と定義の内容を保持する()
    {
        // Arrange
        var definition = new DataDefinition([10, 20, 30, 40], null, 0);
        var dropped = new DataInstance(definition);
        var retained = new DataInstance(definition);
        var memory = new WasmMemory(new WasmLimits(1));
        var actual = new byte[4];

        // Act
        dropped.Drop();
        var recreated = new DataInstance(definition);
        var success = retained.TryInitialize(memory, 0, 0, 4);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(dropped.Length).IsEqualTo(0u);
            await Assert.That(retained.Length).IsEqualTo(4u);
            await Assert.That(recreated.Length).IsEqualTo(4u);
            await Assert.That(success).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 10, 20, 30, 40 })).IsTrue();
            await Assert
                .That(definition.Bytes.AsSpan().SequenceEqual(new byte[] { 10, 20, 30, 40 }))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(4)]
    public async Task 同じデータセグメントを繰り返し破棄する_空のまま成功する(int length)
    {
        // Arrange
        var data = new DataInstance(new DataDefinition(new byte[length], null, 0));
        data.Drop();
        var memory = new WasmMemory(new WasmLimits(0));

        // Act
        data.Drop();
        var success = data.TryInitialize(memory, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(data.Length).IsEqualTo(0u);
            await Assert.That(success).IsTrue();
        }
    }
}
