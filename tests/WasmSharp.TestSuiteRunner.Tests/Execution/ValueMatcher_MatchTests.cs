using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ValueMatcher_MatchTests
{
    [Test]
    public async Task 結果0個を期待し結果も0個_相違なしとする()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var mismatches = Match(state, []);

        // Assert
        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    public async Task 複数の結果が期待と全て一致する_相違なしとする()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        ExpectedValue[] expected =
        [
            Scalar(WasmValueKind.I32, "4294967295"),
            Scalar(WasmValueKind.I64, "18446744073709551615"),
            Scalar(WasmValueKind.F32, "2147483648"),
            Scalar(WasmValueKind.F64, "0"),
            Scalar(WasmValueKind.FuncRef, "null"),
            Scalar(WasmValueKind.ExternRef, "null"),
            Scalar(WasmValueKind.ExternRef, "1"),
        ];

        // Act
        var mismatches = Match(
            state,
            expected,
            WasmValue.FromI32(-1),
            WasmValue.FromI64(-1),
            WasmValue.FromF32Bits(0x80000000),
            WasmValue.FromF64Bits(0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
            WasmValue.FromExternRef(state.GetExternref(1))
        );

        // Assert
        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task 結果の個数が異なる_値を比較せず個数の相違だけを返す(int count)
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var actual = Enumerable.Repeat(WasmValue.FromI32(2), count).ToArray();

        // Act
        var mismatches = Match(state, [Scalar(WasmValueKind.I32, "1")], actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(Positions(mismatches).SequenceEqual([(ValueMismatchKind.Count, null, null)]))
                .IsTrue();
            await Assert.That(mismatches[0].Message).Contains($"{count}個");
        }
    }

    [Test]
    public async Task 同じ位置の結果の型が異なる_nullを含め型の相違を返し値を比較しない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        ExpectedValue[] expected =
        [
            Scalar(WasmValueKind.I32, "0"),
            Scalar(WasmValueKind.FuncRef, "null"),
            Scalar(WasmValueKind.ExternRef, "null"),
            Scalar(WasmValueKind.F32, "0"),
        ];

        // Act
        var mismatches = Match(
            state,
            expected,
            WasmValue.FromI64(0),
            WasmValue.FromExternRef(null),
            WasmValue.FromExternRef(state.GetExternref(1)),
            WasmValue.FromF64Bits(0)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    Positions(mismatches)
                        .SequenceEqual([
                            (ValueMismatchKind.Type, 0, null),
                            (ValueMismatchKind.Type, 1, null),
                            (ValueMismatchKind.Type, 3, null),
                        ])
                )
                .IsTrue();
            await Assert.That(mismatches[1].Message).Contains("funcref");
            await Assert.That(mismatches[1].Message).Contains("externref");
        }
    }

    [Test]
    public async Task 結果の順序が期待と異なる_入れ替わった位置の型の相違を返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var mismatches = Match(
            state,
            [Scalar(WasmValueKind.I32, "1"), Scalar(WasmValueKind.I64, "2")],
            WasmValue.FromI64(2),
            WasmValue.FromI32(1)
        );

        // Assert
        await Assert
            .That(
                Positions(mismatches)
                    .SequenceEqual([
                        (ValueMismatchKind.Type, 0, null),
                        (ValueMismatchKind.Type, 1, null),
                    ])
            )
            .IsTrue();
    }

    [Test]
    public async Task 整数の結果のビット列が異なる_相違した位置の値の相違を全て返す()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        ExpectedValue[] expected =
        [
            Scalar(WasmValueKind.I32, "1"),
            Scalar(WasmValueKind.I64, "9223372036854775808"),
            Scalar(WasmValueKind.I32, "4294967295"),
            Scalar(WasmValueKind.I64, "1"),
        ];

        // Act
        var mismatches = Match(
            state,
            expected,
            WasmValue.FromI32(1),
            WasmValue.FromI64(long.MaxValue),
            WasmValue.FromI32(-1),
            WasmValue.FromI64(0x1_0000_0001)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    Positions(mismatches)
                        .SequenceEqual([
                            (ValueMismatchKind.Value, 1, null),
                            (ValueMismatchKind.Value, 3, null),
                        ])
                )
                .IsTrue();
            await Assert.That(mismatches[0].Message).Contains("0x8000000000000000");
            await Assert.That(mismatches[0].Message).Contains("0x7fffffffffffffff");
        }
    }

    [Test]
    [Arguments(WasmValueKind.F32, "0", 0x80000000UL, false)]
    [Arguments(WasmValueKind.F32, "2147483648", 0x80000000UL, true)]
    [Arguments(WasmValueKind.F32, "2143289345", 0x7FC00001UL, true)]
    [Arguments(WasmValueKind.F32, "2143289345", 0x7FC00000UL, false)]
    [Arguments(WasmValueKind.F32, "2139095041", 0x7FC00001UL, false)]
    [Arguments(WasmValueKind.F32, "2143289344", 0xFFC00000UL, false)]
    [Arguments(WasmValueKind.F64, "0", 0x8000000000000000UL, false)]
    [Arguments(WasmValueKind.F64, "9223372036854775808", 0x8000000000000000UL, true)]
    [Arguments(WasmValueKind.F64, "9221120237041090561", 0x7FF8000000000001UL, true)]
    [Arguments(WasmValueKind.F64, "9221120237041090561", 0x7FF8000000000000UL, false)]
    public async Task 浮動小数点数の結果を具体値と比較する_正負の0とNaNのpayloadを全ビットで区別する(
        WasmValueKind kind,
        string expected,
        ulong actual,
        bool matched
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var value =
            kind == WasmValueKind.F32
                ? WasmValue.FromF32Bits((uint)actual)
                : WasmValue.FromF64Bits(actual);

        // Act
        var mismatches = Match(state, [Scalar(kind, expected)], value);

        // Assert
        await Assert.That(Positions(mismatches).SequenceEqual(Expected(matched))).IsTrue();
    }

    [Test]
    [Arguments("1", "1", true)]
    [Arguments("1", "2", false)]
    [Arguments("1", "null", false)]
    [Arguments("1", "unknown", false)]
    [Arguments("null", "null", true)]
    [Arguments("null", "1", false)]
    public async Task Externrefの結果を比較する_nullかどうかと同じ番号に割り当てたホスト値との同一性で判定する(
        string expected,
        string actual,
        bool matched
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var reference = actual switch
        {
            "null" => null,
            "unknown" => new object(),
            _ => state.GetExternref(uint.Parse(actual)),
        };

        // Act
        var mismatches = Match(
            state,
            [Scalar(WasmValueKind.ExternRef, expected)],
            WasmValue.FromExternRef(reference)
        );

        // Assert
        await Assert.That(Positions(mismatches).SequenceEqual(Expected(matched))).IsTrue();
    }

    [Test]
    public async Task Funcrefの結果をnullと比較する_null参照だけを一致とする()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var function = WasmFunction.CreateHost(new([], []), _ => new WasmResults([]));

        // Act
        var mismatches = Match(
            state,
            [Scalar(WasmValueKind.FuncRef, "null"), Scalar(WasmValueKind.FuncRef, "null")],
            WasmValue.FromFuncRef(null),
            WasmValue.FromFuncRef(function)
        );

        // Assert
        await Assert
            .That(Positions(mismatches).SequenceEqual([(ValueMismatchKind.Value, 1, null)]))
            .IsTrue();
    }

    [Test]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0x7FC00000UL, true)]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0xFFC00000UL, true)]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0x7FC00001UL, false)]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0x7FE00000UL, false)]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0x7FA00000UL, false)]
    [Arguments(WasmValueKind.F32, "nan:canonical", 0x7F800000UL, false)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x7FC00000UL, true)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0xFFC00000UL, true)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x7FC00001UL, true)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0xFFFFFFFFUL, true)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x7FA00000UL, false)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x7F800001UL, false)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x7F800000UL, false)]
    [Arguments(WasmValueKind.F32, "nan:arithmetic", 0x3FC00000UL, false)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0x7FF8000000000000UL, true)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0xFFF8000000000000UL, true)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0x7FF8000000000001UL, false)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0x7FFC000000000000UL, false)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0x7FF4000000000000UL, false)]
    [Arguments(WasmValueKind.F64, "nan:canonical", 0x7FF0000000000000UL, false)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0x7FF8000000000000UL, true)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0xFFF8000000000001UL, true)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0xFFFFFFFFFFFFFFFFUL, true)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0x7FF4000000000000UL, false)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0x7FF0000000000001UL, false)]
    [Arguments(WasmValueKind.F64, "nan:arithmetic", 0x3FF8000000000000UL, false)]
    public async Task 浮動小数点数の結果をNaNPatternと比較する_符号を問わずcanonicalとarithmeticの条件で判定する(
        WasmValueKind kind,
        string pattern,
        ulong actual,
        bool matched
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var value =
            kind == WasmValueKind.F32
                ? WasmValue.FromF32Bits((uint)actual)
                : WasmValue.FromF64Bits(actual);

        // Act
        var mismatches = Match(state, [Scalar(kind, pattern)], value);

        // Assert
        await Assert.That(Positions(mismatches).SequenceEqual(Expected(matched))).IsTrue();
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
            "15",
        },
        0x07060504030201FFUL,
        0xFF0E0D0C0B0A0908UL,
        new[] { 0, 15 }
    )]
    [Arguments(
        LaneType.I16,
        new[] { "1", "2", "3", "4", "5", "6", "7", "8" },
        0x0004000300020001UL,
        0x0008000700FF0005UL,
        new[] { 5 }
    )]
    [Arguments(
        LaneType.I32,
        new[] { "1", "2", "3", "4294967295" },
        0x0000000200000001UL,
        0xFFFFFFFF00000003UL,
        new int[] { }
    )]
    [Arguments(
        LaneType.I32,
        new[] { "1", "2", "3", "4294967295" },
        0x0000000200000001UL,
        0xFFFFFFFE00000003UL,
        new[] { 3 }
    )]
    [Arguments(
        LaneType.I64,
        new[] { "1", "18446744073709551615" },
        0x1UL,
        0x7FFFFFFFFFFFFFFFUL,
        new[] { 1 }
    )]
    [Arguments(
        LaneType.F32,
        new[] { "nan:canonical", "nan:arithmetic", "0", "2147483648" },
        0x7FC00001FFC00000UL,
        0x8000000000000000UL,
        new int[] { }
    )]
    [Arguments(
        LaneType.F32,
        new[] { "nan:canonical", "nan:arithmetic", "0", "2147483648" },
        0x7FA000007FC00001UL,
        0x0000000080000000UL,
        new[] { 0, 1, 2, 3 }
    )]
    [Arguments(
        LaneType.F64,
        new[] { "nan:canonical", "nan:arithmetic" },
        0xFFF8000000000000UL,
        0x7FF8000000000001UL,
        new int[] { }
    )]
    [Arguments(
        LaneType.F64,
        new[] { "nan:canonical", "nan:arithmetic" },
        0x7FF8000000000001UL,
        0x7FF4000000000000UL,
        new[] { 0, 1 }
    )]
    [Arguments(
        LaneType.F64,
        new[] { "9221120237041090561", "9223372036854775808" },
        0x7FF8000000000001UL,
        0x8000000000000000UL,
        new int[] { }
    )]
    [Arguments(
        LaneType.F64,
        new[] { "9221120237041090561", "0" },
        0x7FF8000000000000UL,
        0x8000000000000000UL,
        new[] { 0, 1 }
    )]
    public async Task V128の結果をlaneごとに比較する_期待lane型で分割し具体値とNaNPatternに相違したlaneを返す(
        LaneType laneType,
        string[] lanes,
        ulong low64,
        ulong high64,
        int[] mismatchedLanes
    )
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var mismatches = Match(state, [Vector(laneType, lanes)], WasmValue.FromV128(low64, high64));

        // Assert
        await Assert
            .That(
                Positions(mismatches)
                    .SequenceEqual(
                        mismatchedLanes.Select(x => (ValueMismatchKind.Value, (int?)0, (int?)x))
                    )
            )
            .IsTrue();
    }

    [Test]
    public async Task 複数の結果のv128のlaneがNaNPatternと一致しない_結果とlaneの位置と期待と実際を示す()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var mismatches = Match(
            state,
            [Scalar(WasmValueKind.I32, "0"), Vector(LaneType.F32, "0", "nan:arithmetic", "0", "0")],
            WasmValue.FromI32(0),
            WasmValue.FromV128(0x7FA0000000000000, 0)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(Positions(mismatches).SequenceEqual([(ValueMismatchKind.Value, 1, 1)]))
                .IsTrue();
            await Assert.That(mismatches[0].Message).Contains("lane1");
            await Assert.That(mismatches[0].Message).Contains("nan:arithmetic");
            await Assert.That(mismatches[0].Message).Contains("0x7fa00000");
        }
    }

    private static ImmutableArray<ValueMismatch> Match(
        ScriptState state,
        ExpectedValue[] expected,
        params WasmValue[] actual
    )
    {
        return ValueMatcher.Match(
            ValueMatcher.Parse([.. expected]),
            new WasmResults(actual),
            state
        );
    }

    private static (ValueMismatchKind, int?, int?)[] Expected(bool matched)
    {
        return matched ? [] : [(ValueMismatchKind.Value, 0, null)];
    }

    private static ExpectedValue Vector(LaneType laneType, params string[] lanes)
    {
        return new(WasmValueKind.V128, null, laneType, [.. lanes]);
    }

    private static ExpectedValue Scalar(WasmValueKind kind, string value)
    {
        return new(kind, value, null, []);
    }

    private static IEnumerable<(ValueMismatchKind, int?, int?)> Positions(
        ImmutableArray<ValueMismatch> mismatches
    )
    {
        return mismatches.Select(x => (x.Kind, x.Index, x.Lane));
    }
}
