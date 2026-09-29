using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ValueCodec_RecordTests
{
    [Test]
    [Arguments(WasmValueKind.I32, 0x1UL, "i32", "00000001")]
    [Arguments(WasmValueKind.I32, 0xFFFFFFFFUL, "i32", "ffffffff")]
    [Arguments(WasmValueKind.I64, 0x1UL, "i64", "0000000000000001")]
    [Arguments(WasmValueKind.I64, 0x8000000000000000UL, "i64", "8000000000000000")]
    [Arguments(WasmValueKind.F32, 0x0UL, "f32", "00000000")]
    [Arguments(WasmValueKind.F32, 0x80000000UL, "f32", "80000000")]
    [Arguments(WasmValueKind.F32, 0x7F800001UL, "f32", "7f800001")]
    [Arguments(WasmValueKind.F64, 0x8000000000000000UL, "f64", "8000000000000000")]
    [Arguments(WasmValueKind.F64, 0x7FF0000000000001UL, "f64", "7ff0000000000001")]
    public async Task 数値を記録する_型名と幅を固定した小文字hexのビット列を記録する(
        WasmValueKind kind,
        ulong bits,
        string type,
        string expected
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var value = kind switch
        {
            WasmValueKind.I32 => WasmValue.FromI32(unchecked((int)(uint)bits)),
            WasmValueKind.I64 => WasmValue.FromI64(unchecked((long)bits)),
            WasmValueKind.F32 => WasmValue.FromF32Bits((uint)bits),
            _ => WasmValue.FromF64Bits(bits),
        };

        // Act
        var record = ValueCodec.Record(value, state);

        // Assert
        await Assert.That(record).IsEqualTo(new ValueRecord(type) { Bits = expected });
    }

    [Test]
    [Arguments(0x0706050403020100UL, 0xFF0E0D0C0B0A0908UL, "0706050403020100", "ff0e0d0c0b0a0908")]
    [Arguments(0x1UL, 0x0UL, "0000000000000001", "0000000000000000")]
    public async Task V128を記録する_CPUのendianに依存しない下位と上位64ビットの小文字hexを記録する(
        ulong low64,
        ulong high64,
        string expectedLow64,
        string expectedHigh64
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var record = ValueCodec.Record(WasmValue.FromV128(low64, high64), state);

        // Assert
        await Assert
            .That(record)
            .IsEqualTo(new ValueRecord("v128") { Low64 = expectedLow64, High64 = expectedHigh64 });
    }

    [Test]
    public async Task Null参照を記録する_型とnullを記録しtokenを割り当てない()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var funcref = ValueCodec.Record(WasmValue.FromFuncRef(null), state);
        var externref = ValueCodec.Record(WasmValue.FromExternRef(null), state);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(funcref).IsEqualTo(new ValueRecord("funcref") { IsNull = true });
            await Assert.That(externref).IsEqualTo(new ValueRecord("externref") { IsNull = true });
            await Assert.That(state.GetReferenceToken(new object())).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 割り当て済みのexternrefを記録する_初出順のtokenと元の番号を記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var five = state.GetExternref(5);
        var three = state.GetExternref(3);

        // Act
        ValueRecord[] records =
        [
            ValueCodec.Record(WasmValue.FromExternRef(three), state),
            ValueCodec.Record(WasmValue.FromExternRef(five), state),
            ValueCodec.Record(WasmValue.FromExternRef(three), state),
        ];

        // Assert
        await Assert
            .That(
                records.SequenceEqual([
                    Reference("externref", 0, 3),
                    Reference("externref", 1, 5),
                    Reference("externref", 0, 3),
                ])
            )
            .IsTrue();
    }

    [Test]
    public async Task 割り当てていない非null参照を記録する_番号を持たず同じ参照に同じtokenを記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var function = WasmFunction.CreateHost(new([], []), _ => new WasmResults([]));
        var unknown = new object();

        // Act
        ValueRecord[] records =
        [
            ValueCodec.Record(WasmValue.FromFuncRef(function), state),
            ValueCodec.Record(WasmValue.FromExternRef(unknown), state),
            ValueCodec.Record(WasmValue.FromFuncRef(function), state),
        ];

        // Assert
        await Assert
            .That(
                records.SequenceEqual([
                    Reference("funcref", 0, null),
                    Reference("externref", 1, null),
                    Reference("funcref", 0, null),
                ])
            )
            .IsTrue();
    }

    private static ValueRecord Reference(string type, int token, uint? externrefNumber)
    {
        return new(type)
        {
            IsNull = false,
            Token = token,
            ExternrefNumber = externrefNumber,
        };
    }
}
