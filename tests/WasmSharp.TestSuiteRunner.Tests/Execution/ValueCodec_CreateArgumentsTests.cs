using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ValueCodec_CreateArgumentsTests
{
    [Test]
    [Arguments(WasmValueKind.I32, "0", 0x0UL)]
    [Arguments(WasmValueKind.I32, "2147483648", 0x80000000UL)]
    [Arguments(WasmValueKind.I32, "4294967295", 0xFFFFFFFFUL)]
    [Arguments(WasmValueKind.I64, "9223372036854775808", 0x8000000000000000UL)]
    [Arguments(WasmValueKind.I64, "18446744073709551615", 0xFFFFFFFFFFFFFFFFUL)]
    [Arguments(WasmValueKind.F32, "0", 0x0UL)]
    [Arguments(WasmValueKind.F32, "2147483648", 0x80000000UL)]
    [Arguments(WasmValueKind.F32, "2139095041", 0x7F800001UL)]
    [Arguments(WasmValueKind.F32, "4294967295", 0xFFFFFFFFUL)]
    [Arguments(WasmValueKind.F64, "9223372036854775808", 0x8000000000000000UL)]
    [Arguments(WasmValueKind.F64, "9218868437227405313", 0x7FF0000000000001UL)]
    [Arguments(WasmValueKind.F64, "18446744073709551615", 0xFFFFFFFFFFFFFFFFUL)]
    public async Task 数値を符号なし10進数で指定する_同じ型と幅のビット列を変更せずに構築する(
        WasmValueKind kind,
        string text,
        ulong expected
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var values = ValueCodec.CreateArguments([Scalar(kind, text)], state);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values.Length).IsEqualTo(1);
            await Assert.That(values[0].Kind).IsEqualTo(kind);
            await Assert.That(GetBits(values[0])).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task 複数の引数を指定する_JSONの順序と型を保って構築する()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var values = ValueCodec.CreateArguments(
            [
                Scalar(WasmValueKind.F64, "1"),
                Scalar(WasmValueKind.ExternRef, "null"),
                Scalar(WasmValueKind.I32, "2"),
                Scalar(WasmValueKind.I64, "3"),
            ],
            state
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    values
                        .Select(x => x.Kind)
                        .SequenceEqual([
                            WasmValueKind.F64,
                            WasmValueKind.ExternRef,
                            WasmValueKind.I32,
                            WasmValueKind.I64,
                        ])
                )
                .IsTrue();
            await Assert.That(values[0].AsF64Bits()).IsEqualTo(1UL);
            await Assert.That(values[2].AsI32()).IsEqualTo(2);
            await Assert.That(values[3].AsI64()).IsEqualTo(3L);
        }
    }

    [Test]
    public async Task 参照のnullを指定する_同じ型のnull参照を構築する()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var values = ValueCodec.CreateArguments(
            [Scalar(WasmValueKind.FuncRef, "null"), Scalar(WasmValueKind.ExternRef, "null")],
            state
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].Kind).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(values[0].AsFuncRef()).IsNull();
            await Assert.That(values[1].Kind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(values[1].AsExternRef()).IsNull();
        }
    }

    [Test]
    public async Task Externrefの番号を指定する_入力内で番号ごとに同一のホスト値を割り当てる()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var assigned = state.GetExternref(1);

        // Act
        var values = ValueCodec.CreateArguments(
            [
                Scalar(WasmValueKind.ExternRef, "1"),
                Scalar(WasmValueKind.ExternRef, "2"),
                Scalar(WasmValueKind.ExternRef, "1"),
                Scalar(WasmValueKind.ExternRef, "4294967295"),
            ],
            state
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].AsExternRef()).IsSameReferenceAs(assigned);
            await Assert.That(values[1].AsExternRef()).IsSameReferenceAs(state.GetExternref(2));
            await Assert.That(values[1].AsExternRef()).IsNotSameReferenceAs(assigned);
            await Assert.That(values[2].AsExternRef()).IsSameReferenceAs(assigned);
            await Assert
                .That(values[3].AsExternRef())
                .IsSameReferenceAs(state.GetExternref(uint.MaxValue));
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32, "4294967296")]
    [Arguments(WasmValueKind.I64, "18446744073709551616")]
    [Arguments(WasmValueKind.F32, "4294967296")]
    [Arguments(WasmValueKind.F64, "18446744073709551616")]
    [Arguments(WasmValueKind.I32, "-1")]
    [Arguments(WasmValueKind.I32, "+1")]
    [Arguments(WasmValueKind.I32, " 1")]
    [Arguments(WasmValueKind.I32, "0x1")]
    [Arguments(WasmValueKind.I32, "")]
    [Arguments(WasmValueKind.I32, "007")]
    [Arguments(WasmValueKind.I32, "1\0")]
    [Arguments(WasmValueKind.F32, "1.5")]
    [Arguments(WasmValueKind.F32, "nan:canonical")]
    [Arguments(WasmValueKind.F64, "nan:arithmetic")]
    [Arguments(WasmValueKind.ExternRef, "4294967296")]
    [Arguments(WasmValueKind.ExternRef, "Null")]
    [Arguments(WasmValueKind.FuncRef, "0")]
    public async Task 固定形式にない値の文字列を指定する_切り詰めや推測をせず位置を示して拒否する(
        WasmValueKind kind,
        string text
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ValueCodec.CreateArguments(
                    [Scalar(WasmValueKind.I32, "0"), Scalar(kind, text)],
                    state
                )
            )
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("action.args[1].value");
    }

    [Test]
    [Arguments(
        LaneType.I8,
        new[]
        {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "255",
        },
        0x0706050403020100UL,
        0xFF0E0D0C0B0A0908UL
    )]
    [Arguments(
        LaneType.I16,
        new[] { "1", "2", "3", "4", "5", "6", "7", "65535" },
        0x0004000300020001UL,
        0xFFFF000700060005UL
    )]
    [Arguments(
        LaneType.I32,
        new[] { "1", "2", "3", "4294967295" },
        0x0000000200000001UL,
        0xFFFFFFFF00000003UL
    )]
    [Arguments(LaneType.I64, new[] { "1", "18446744073709551615" }, 0x1UL, 0xFFFFFFFFFFFFFFFFUL)]
    [Arguments(
        LaneType.F32,
        new[] { "2147483648", "2139095041", "0", "4286578689" },
        0x7F80000180000000UL,
        0xFF80000100000000UL
    )]
    [Arguments(
        LaneType.F64,
        new[] { "9223372036854775808", "9218868437227405313" },
        0x8000000000000000UL,
        0x7FF0000000000001UL
    )]
    public async Task V128の各lane型でlane0から順に指定する_lane0を下位に置き各laneのビット列を保って構築する(
        LaneType laneType,
        string[] lanes,
        ulong low64,
        ulong high64
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var values = ValueCodec.CreateArguments([Vector(laneType, lanes)], state);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(values[0].AsV128()).IsEqualTo((low64, high64));
        }
    }

    [Test]
    [Arguments(LaneType.I8, 16, "256")]
    [Arguments(LaneType.I16, 8, "65536")]
    [Arguments(LaneType.I32, 4, "4294967296")]
    [Arguments(LaneType.I64, 2, "18446744073709551616")]
    [Arguments(LaneType.F32, 4, "4294967296")]
    [Arguments(LaneType.F64, 2, "18446744073709551616")]
    [Arguments(LaneType.I8, 16, "-1")]
    [Arguments(LaneType.I16, 8, "01")]
    [Arguments(LaneType.F32, 4, "nan:canonical")]
    [Arguments(LaneType.F64, 2, "nan:arithmetic")]
    public async Task V128のlaneにlane幅の符号なし10進数ではない値を指定する_切り詰めずlaneの位置を示して拒否する(
        LaneType laneType,
        int count,
        string lane
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var lanes = Enumerable.Repeat("0", count).ToArray();
        lanes[1] = lane;

        // Act & Assert
        var exception = await Assert
            .That(() => ValueCodec.CreateArguments([Vector(laneType, lanes)], state))
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("action.args[0].value[1]");
    }

    [Test]
    [Arguments(LaneType.I8, 15)]
    [Arguments(LaneType.I8, 17)]
    [Arguments(LaneType.I16, 16)]
    [Arguments(LaneType.I32, 2)]
    [Arguments(LaneType.I64, 4)]
    [Arguments(LaneType.I64, 0)]
    [Arguments(LaneType.F32, 8)]
    [Arguments(LaneType.F64, 1)]
    public async Task V128のlane数がlane型と一致しない_位置を示して拒否する(
        LaneType laneType,
        int count
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var lanes = Enumerable.Repeat("0", count).ToArray();

        // Act & Assert
        var exception = await Assert
            .That(() => ValueCodec.CreateArguments([Vector(laneType, lanes)], state))
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("action.args[0].value");
    }

    private static ArgumentValue Scalar(WasmValueKind kind, string value)
    {
        return new(kind, value, null, []);
    }

    private static ArgumentValue Vector(LaneType laneType, string[] lanes)
    {
        return new(WasmValueKind.V128, null, laneType, [.. lanes]);
    }

    private static ulong GetBits(WasmValue value)
    {
        return value.Kind switch
        {
            WasmValueKind.I32 => unchecked((uint)value.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
            WasmValueKind.F32 => value.AsF32Bits(),
            _ => value.AsF64Bits(),
        };
    }
}
