using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ValueMatcher_ParseTests
{
    [Test]
    public async Task 具体値と参照の期待値を指定する_型の幅のビット列と参照の期待へ解析する()
    {
        // Arrange
        ExpectedValue[] expected =
        [
            Scalar(WasmValueKind.I32, "4294967295"),
            Scalar(WasmValueKind.I64, "1"),
            Scalar(WasmValueKind.F32, "2147483648"),
            Scalar(WasmValueKind.F64, "9218868437227405313"),
            Scalar(WasmValueKind.FuncRef, "null"),
            Scalar(WasmValueKind.ExternRef, "null"),
            Scalar(WasmValueKind.ExternRef, "7"),
        ];

        // Act
        var patterns = ValueMatcher.Parse([.. expected]);

        // Assert
        await Assert
            .That(
                patterns
                    .Select(Format)
                    .SequenceEqual([
                        "I32/32/ffffffff:-/-",
                        "I64/64/1:-/-",
                        "F32/32/80000000:-/-",
                        "F64/64/7ff0000000000001:-/-",
                        "FuncRef/0//-",
                        "ExternRef/0//-",
                        "ExternRef/0//7",
                    ])
            )
            .IsTrue();
    }

    [Test]
    [Arguments(WasmValueKind.I32, "4294967296")]
    [Arguments(WasmValueKind.I64, "18446744073709551616")]
    [Arguments(WasmValueKind.F32, "4294967296")]
    [Arguments(WasmValueKind.F64, "-1")]
    [Arguments(WasmValueKind.F32, "1.5")]
    [Arguments(WasmValueKind.I32, "007")]
    [Arguments(WasmValueKind.I32, "nan:canonical")]
    [Arguments(WasmValueKind.I64, "nan:arithmetic")]
    [Arguments(WasmValueKind.FuncRef, "0")]
    [Arguments(WasmValueKind.ExternRef, "4294967296")]
    [Arguments(WasmValueKind.ExternRef, "Null")]
    public async Task 固定形式にない期待値の文字列を指定する_位置を示して拒否する(
        WasmValueKind kind,
        string text
    )
    {
        // Act & Assert
        var exception = await Assert
            .That(() => ValueMatcher.Parse([Scalar(WasmValueKind.I32, "0"), Scalar(kind, text)]))
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("expected[1].value");
    }

    [Test]
    public async Task NaNPatternとv128の期待値を指定する_NaNPatternとlane幅ごとの期待へ解析する()
    {
        // Arrange
        ExpectedValue[] expected =
        [
            Scalar(WasmValueKind.F32, "nan:canonical"),
            Scalar(WasmValueKind.F64, "nan:arithmetic"),
            Vector(LaneType.F32, "nan:canonical", "nan:arithmetic", "0", "2147483648"),
            Vector(LaneType.F64, "nan:arithmetic", "9221120237041090561"),
            Vector(LaneType.I16, "1", "2", "3", "4", "5", "6", "7", "65535"),
        ];

        // Act
        var patterns = ValueMatcher.Parse([.. expected]);

        // Assert
        await Assert
            .That(
                patterns
                    .Select(Format)
                    .SequenceEqual([
                        "F32/32/0:Canonical/-",
                        "F64/64/0:Arithmetic/-",
                        "V128/32/0:Canonical,0:Arithmetic,0:-,80000000:-/-",
                        "V128/64/0:Arithmetic,7ff8000000000001:-/-",
                        "V128/16/1:-,2:-,3:-,4:-,5:-,6:-,7:-,ffff:-/-",
                    ])
            )
            .IsTrue();
    }

    [Test]
    [Arguments(WasmValueKind.F32, "nan:Canonical")]
    [Arguments(WasmValueKind.F32, "nan")]
    [Arguments(WasmValueKind.F64, "nan:0x1")]
    [Arguments(WasmValueKind.F64, " nan:arithmetic")]
    public async Task 固定形式にないNaNPatternを指定する_位置を示して拒否する(
        WasmValueKind kind,
        string text
    )
    {
        // Act & Assert
        var exception = await Assert
            .That(() => ValueMatcher.Parse([Scalar(WasmValueKind.I32, "0"), Scalar(kind, text)]))
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("expected[1].value");
    }

    [Test]
    [Arguments(LaneType.I8, 16, "256")]
    [Arguments(LaneType.I16, 8, "65536")]
    [Arguments(LaneType.I32, 4, "nan:canonical")]
    [Arguments(LaneType.I64, 2, "nan:arithmetic")]
    [Arguments(LaneType.F32, 4, "4294967296")]
    [Arguments(LaneType.F32, 4, "nan:Arithmetic")]
    [Arguments(LaneType.F64, 2, "18446744073709551616")]
    public async Task V128のlaneに固定形式にない期待値を指定する_laneの位置を示して拒否する(
        LaneType laneType,
        int count,
        string lane
    )
    {
        // Arrange
        var lanes = Enumerable.Repeat("0", count).ToArray();
        lanes[1] = lane;

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ValueMatcher.Parse([Scalar(WasmValueKind.I32, "0"), Vector(laneType, lanes)])
            )
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("expected[1].value[1]");
    }

    [Test]
    [Arguments(LaneType.I8, 15)]
    [Arguments(LaneType.I16, 16)]
    [Arguments(LaneType.I32, 5)]
    [Arguments(LaneType.I64, 1)]
    [Arguments(LaneType.F32, 2)]
    [Arguments(LaneType.F64, 4)]
    public async Task V128のlane数がlane型と一致しない期待値を指定する_位置を示して拒否する(
        LaneType laneType,
        int count
    )
    {
        // Arrange
        var lanes = Enumerable.Repeat("0", count).ToArray();

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ValueMatcher.Parse([Scalar(WasmValueKind.I32, "0"), Vector(laneType, lanes)])
            )
            .ThrowsExactly<ScriptFormatException>();
        await Assert.That(exception!.Message).Contains("expected[1].value");
    }

    private static ExpectedValue Vector(LaneType laneType, params string[] lanes)
    {
        return new(WasmValueKind.V128, null, laneType, [.. lanes]);
    }

    private static ExpectedValue Scalar(WasmValueKind kind, string value)
    {
        return new(kind, value, null, []);
    }

    private static string Format(ValuePattern pattern)
    {
        var bits = string.Join(
            ",",
            pattern.Bits.Select(x => $"{x.Bits:x}:{x.Nan?.ToString() ?? "-"}")
        );
        return $"{pattern.Kind}/{pattern.Width}/{bits}/{pattern.Externref?.ToString() ?? "-"}";
    }
}
