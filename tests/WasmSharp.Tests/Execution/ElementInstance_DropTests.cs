using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class ElementInstance_DropTests
{
    [Test]
    public async Task 同じ参照列から生成した片方を破棄する_他方と転送済み参照と入力列を保持する()
    {
        // Arrange
        var reference = new object();
        var values = new[] { WasmValue.FromExternRef(reference) };
        var dropped = new ElementInstance(values);
        var retained = new ElementInstance(values);
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(2));
        var firstSuccess = dropped.TryInitialize(table, 0, 0, 1);

        // Act
        dropped.Drop();
        var secondSuccess = retained.TryInitialize(table, 1, 0, 1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(dropped.Length).IsEqualTo(0u);
            await Assert.That(retained.Length).IsEqualTo(1u);
            await Assert.That(firstSuccess).IsTrue();
            await Assert.That(secondSuccess).IsTrue();
            await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
            await Assert.That(ReferenceEquals(table.Get(1).AsExternRef(), reference)).IsTrue();
            await Assert.That(ReferenceEquals(values[0].AsExternRef(), reference)).IsTrue();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task 同じ要素セグメントを繰り返し破棄する_空のまま成功する(int length)
    {
        // Arrange
        var values = new WasmValue[length];
        Array.Fill(values, WasmValue.FromExternRef(new object()));
        var element = new ElementInstance(values);
        element.Drop();
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(0));

        // Act
        element.Drop();
        var success = element.TryInitialize(table, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(element.Length).IsEqualTo(0u);
            await Assert.That(success).IsTrue();
        }
    }
}
