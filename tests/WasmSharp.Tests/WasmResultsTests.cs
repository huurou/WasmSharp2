namespace WasmSharp.Tests;

internal class WasmResults_ConstructorTests
{
    [Test]
    public async Task 空の配列を指定する_戻り値のコレクションが空になる()
    {
        // Arrange
        WasmValue[] values = [];

        // Act
        var results = new WasmResults(values);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Values.IsDefault).IsFalse();
            await Assert.That(results.Values.Length).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 構築後に元の配列を変更する_戻り値の個数と順序と内容を保持する()
    {
        // Arrange
        var reference = new object();
        WasmValue[] values =
        [
            WasmValue.FromI32(-1),
            WasmValue.FromI64(0x100000000L),
            WasmValue.FromF32Bits(0xFFC12345U),
            WasmValue.FromF64Bits(0x8000000000000000UL),
            WasmValue.FromV128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(reference),
        ];

        // Act
        var results = new WasmResults(values);
        Array.Fill(values, default);
        var actual = results.Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Length).IsEqualTo(7);
            await Assert.That(actual[0].AsI32()).IsEqualTo(-1);
            await Assert.That(actual[1].AsI64()).IsEqualTo(0x100000000L);
            await Assert.That(actual[2].AsF32Bits()).IsEqualTo(0xFFC12345U);
            await Assert.That(actual[3].AsF64Bits()).IsEqualTo(0x8000000000000000UL);
            await Assert
                .That(actual[4].AsV128())
                .IsEqualTo((0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL));
            await Assert.That(actual[5].AsFuncRef()).IsNull();
            await Assert.That(ReferenceEquals(actual[6].AsExternRef(), reference)).IsTrue();
        }
    }
}
