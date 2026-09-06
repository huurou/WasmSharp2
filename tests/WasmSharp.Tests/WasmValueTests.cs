namespace WasmSharp.Tests;

public class WasmValue_FromI32Tests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task 整数を構築する_種類と値を保持する(int expected)
    {
        // Arrange
        var input = expected;

        // Act
        var value = WasmValue.FromI32(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(value.AsI32()).IsEqualTo(expected);
        }
    }
}

public class WasmValue_AsI32Tests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsI32).ThrowsExactly<InvalidOperationException>();
            }
        }
    }

    [Test]
    public async Task 既定値を取得する_I32の0を返す()
    {
        // Arrange
        var value = default(WasmValue);

        // Act
        var actual = value.AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual).IsEqualTo(0);
        }
    }
}

public class WasmValue_FromI64Tests
{
    [Test]
    [Arguments(0L)]
    [Arguments(1L)]
    [Arguments(-1L)]
    [Arguments(0x100000000L)]
    [Arguments(-0x100000001L)]
    [Arguments(long.MinValue)]
    [Arguments(long.MaxValue)]
    public async Task 整数を構築する_種類と64ビットの値を保持する(long expected)
    {
        // Arrange
        var input = expected;

        // Act
        var value = WasmValue.FromI64(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(value.AsI64()).IsEqualTo(expected);
        }
    }
}

public class WasmValue_AsI64Tests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI32(0),
            WasmValue.FromF32(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsI64).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_FromF32Tests
{
    [Test]
    [Arguments(0x00000000U)]
    [Arguments(0x80000000U)]
    [Arguments(0x7F800000U)]
    [Arguments(0xFF800000U)]
    [Arguments(0x3FC00000U)]
    [Arguments(0xBFC00000U)]
    [Arguments(0x00000001U)]
    [Arguments(0x7F7FFFFFU)]
    [Arguments(0x7FC00001U)]
    [Arguments(0x7FC12345U)]
    [Arguments(0xFFC12345U)]
    [Arguments(0x7F800001U)]
    [Arguments(0xFF800123U)]
    public async Task 浮動小数点数を構築する_種類と元のビット列を保持する(uint expectedBits)
    {
        // Arrange
        var input = BitConverter.UInt32BitsToSingle(expectedBits);

        // Act
        var value = WasmValue.FromF32(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(value.AsF32Bits()).IsEqualTo(expectedBits);
            await Assert
                .That(BitConverter.SingleToUInt32Bits(value.AsF32()))
                .IsEqualTo(expectedBits);
        }
    }
}

public class WasmValue_FromF32BitsTests
{
    [Test]
    [Arguments(0x00000000U)]
    [Arguments(0x80000000U)]
    [Arguments(0x7F800000U)]
    [Arguments(0xFF800000U)]
    [Arguments(0x3FC00000U)]
    [Arguments(0xBFC00000U)]
    [Arguments(0x00000001U)]
    [Arguments(0x7F7FFFFFU)]
    [Arguments(0x7FC00001U)]
    [Arguments(0x7FC12345U)]
    [Arguments(0xFFC12345U)]
    [Arguments(0x7F800001U)]
    [Arguments(0xFF800123U)]
    public async Task ビット列を構築する_種類と元のビット列を保持する(uint expectedBits)
    {
        // Arrange
        var input = expectedBits;

        // Act
        var value = WasmValue.FromF32Bits(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(value.AsF32Bits()).IsEqualTo(expectedBits);
            await Assert
                .That(BitConverter.SingleToUInt32Bits(value.AsF32()))
                .IsEqualTo(expectedBits);
        }
    }
}

public class WasmValue_AsF32Tests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI32(0),
            WasmValue.FromI64(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsF32).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_AsF32BitsTests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI32(0),
            WasmValue.FromI64(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsF32Bits).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_FromF64Tests
{
    [Test]
    [Arguments(0x0000000000000000UL)]
    [Arguments(0x8000000000000000UL)]
    [Arguments(0x7FF0000000000000UL)]
    [Arguments(0xFFF0000000000000UL)]
    [Arguments(0x3FF8000000000000UL)]
    [Arguments(0xBFF8000000000000UL)]
    [Arguments(0x0000000000000001UL)]
    [Arguments(0x7FEFFFFFFFFFFFFFUL)]
    [Arguments(0x7FF8000000000001UL)]
    [Arguments(0x7FF8123456789ABCUL)]
    [Arguments(0xFFF8123456789ABCUL)]
    [Arguments(0x7FF0000000000001UL)]
    [Arguments(0xFFF0123456789ABCUL)]
    public async Task 浮動小数点数を構築する_種類と元のビット列を保持する(ulong expectedBits)
    {
        // Arrange
        var input = BitConverter.UInt64BitsToDouble(expectedBits);

        // Act
        var value = WasmValue.FromF64(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(value.AsF64Bits()).IsEqualTo(expectedBits);
            await Assert
                .That(BitConverter.DoubleToUInt64Bits(value.AsF64()))
                .IsEqualTo(expectedBits);
        }
    }
}

public class WasmValue_FromF64BitsTests
{
    [Test]
    [Arguments(0x0000000000000000UL)]
    [Arguments(0x8000000000000000UL)]
    [Arguments(0x7FF0000000000000UL)]
    [Arguments(0xFFF0000000000000UL)]
    [Arguments(0x3FF8000000000000UL)]
    [Arguments(0xBFF8000000000000UL)]
    [Arguments(0x0000000000000001UL)]
    [Arguments(0x7FEFFFFFFFFFFFFFUL)]
    [Arguments(0x7FF8000000000001UL)]
    [Arguments(0x7FF8123456789ABCUL)]
    [Arguments(0xFFF8123456789ABCUL)]
    [Arguments(0x7FF0000000000001UL)]
    [Arguments(0xFFF0123456789ABCUL)]
    public async Task ビット列を構築する_種類と元のビット列を保持する(ulong expectedBits)
    {
        // Arrange
        var input = expectedBits;

        // Act
        var value = WasmValue.FromF64Bits(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(value.AsF64Bits()).IsEqualTo(expectedBits);
            await Assert
                .That(BitConverter.DoubleToUInt64Bits(value.AsF64()))
                .IsEqualTo(expectedBits);
        }
    }
}

public class WasmValue_AsF64Tests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI32(0),
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsF64).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_AsF64BitsTests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            WasmValue.FromI32(0),
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsF64Bits).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_FromV128Tests
{
    [Test]
    [Arguments(0UL, 0UL)]
    [Arguments(ulong.MaxValue, ulong.MaxValue)]
    [Arguments(ulong.MaxValue, 0UL)]
    [Arguments(0UL, ulong.MaxValue)]
    [Arguments(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL)]
    [Arguments(0x8000000000000000UL, 1UL)]
    public async Task 上下64ビットを構築する_種類と128ビット全体を保持する(
        ulong low64,
        ulong high64
    )
    {
        // Arrange
        var expected = (Low64: low64, High64: high64);

        // Act
        var value = WasmValue.FromV128(low64, high64);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(value.AsV128()).IsEqualTo(expected);
        }
    }
}

public class WasmValue_AsV128Tests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            default,
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromF64(0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsV128).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_FromFuncRefTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 関数参照を構築する_種類とnullまたは参照先を保持する(bool isNull)
    {
        // Arrange
        var expected = isNull ? null : new WasmFunction();

        // Act
        var value = WasmValue.FromFuncRef(expected);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(ReferenceEquals(value.AsFuncRef(), expected)).IsTrue();
        }
    }
}

public class WasmValue_AsFuncRefTests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            default,
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromExternRef(null),
            WasmValue.FromExternRef(new WasmFunction()),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsFuncRef).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}

public class WasmValue_FromExternRefTests
{
    [Test]
    public async Task Nullを構築する_ExternRefの種類とnullを保持する()
    {
        // Arrange
        object? reference = null;

        // Act
        var value = WasmValue.FromExternRef(reference);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(value.AsExternRef()).IsNull();
        }
    }

    [Test]
    public async Task 同じCLRオブジェクトから再構築する_元の参照先を保持する()
    {
        // Arrange
        var reference = new object();

        // Act
        var value = WasmValue.FromExternRef(reference);
        var reconstructed = WasmValue.FromExternRef(reference);
        var roundTrip = WasmValue.FromExternRef(value.AsExternRef());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(ReferenceEquals(value.AsExternRef(), reference)).IsTrue();
            await Assert.That(ReferenceEquals(reconstructed.AsExternRef(), reference)).IsTrue();
            await Assert.That(ReferenceEquals(roundTrip.AsExternRef(), reference)).IsTrue();
        }
    }

    [Test]
    public async Task 同じ内容の別オブジェクトを構築する_それぞれの参照先を保持する()
    {
        // Arrange
        var first = new string('a', 3);
        var second = new string('a', 3);

        // Act
        var firstValue = WasmValue.FromExternRef(first);
        var secondValue = WasmValue.FromExternRef(second);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(firstValue.AsExternRef(), first)).IsTrue();
            await Assert.That(ReferenceEquals(secondValue.AsExternRef(), second)).IsTrue();
            await Assert
                .That(ReferenceEquals(firstValue.AsExternRef(), secondValue.AsExternRef()))
                .IsFalse();
        }
    }
}

public class WasmValue_AsExternRefTests
{
    [Test]
    public async Task 異なる種類を取得する_状態の契約違反として拒否する()
    {
        // Arrange
        WasmValue[] values =
        [
            default,
            WasmValue.FromI64(0),
            WasmValue.FromF32(0),
            WasmValue.FromF64(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromFuncRef(new WasmFunction()),
        ];

        // Act & Assert
        using (Assert.Multiple())
        {
            foreach (var value in values)
            {
                await Assert.That(value.AsExternRef).ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}
