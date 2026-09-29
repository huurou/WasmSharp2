using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assert_returnの期待値と実際の結果を、個数・順序・型・ビット列・参照の同一性で比較する
/// </summary>
/// <remarks>
/// 個数と各位置の型を先に比較し、揃った場合だけ値を比較する。整数と具体的な浮動小数点数は全ビットで比較する。
/// NaN patternはscalarとv128の浮動小数点laneに同じ条件を使い、v128は期待lane型で分割してlaneごとに比較する。
/// 参照は型付きnullか、externrefでは同じ番号に割り当てたホスト値と同じ参照かで比較し、内容の等値性を使わない。
/// </remarks>
internal static class ValueMatcher
{
    private const string CANONICAL = "nan:canonical";
    private const string ARITHMETIC = "nan:arithmetic";

    /// <summary>
    /// f32で指数部がすべて1、仮数部は最上位ビットだけが1のビット列で、符号を除いたcanonical NaNと同じ値
    /// </summary>
    private const ulong F32_QUIET_NAN = 0x7FC00000;

    /// <summary>
    /// f64で指数部がすべて1、仮数部は最上位ビットだけが1のビット列で、符号を除いたcanonical NaNと同じ値
    /// </summary>
    private const ulong F64_QUIET_NAN = 0x7FF8000000000000;

    /// <summary>
    /// 期待値の文字列を検査し、比較用の形へ解析する。
    /// </summary>
    /// <remarks>
    /// actionを実行する前に呼び、不正な期待値のcommandで副作用を起こさないようにする。
    /// </remarks>
    /// <param name="expected">JSONの記載順の値付き期待値</param>
    /// <exception cref="ScriptFormatException">値の文字列が固定形式にない場合</exception>
    internal static ImmutableArray<ValuePattern> Parse(ImmutableArray<ExpectedValue> expected)
    {
        return [.. expected.Select((x, i) => Parse(x, $"expected[{i}].value"))];
    }

    /// <summary>
    /// 解析済みの期待値と実際の結果を比較し、相違箇所を返す。
    /// </summary>
    /// <param name="expected">解析済みの期待値</param>
    /// <param name="actual">invokeの結果、またはgetの現在値を1個持つ結果</param>
    /// <param name="state">externrefの番号と割り当てたホスト値を管理する入力の状態</param>
    /// <returns>相違箇所。全て一致した場合は空</returns>
    internal static ImmutableArray<ValueMismatch> Match(
        ImmutableArray<ValuePattern> expected,
        WasmResults actual,
        ScriptState state
    )
    {
        var values = actual.Values;
        if (expected.Length != values.Length)
        {
            // 位置の対応が定まらないため、型と値を比較しない。
            return
            [
                new(
                    ValueMismatchKind.Count,
                    null,
                    null,
                    $"結果の個数が期待した{expected.Length}個ではなく{values.Length}個です。"
                ),
            ];
        }

        ImmutableArray<ValueMismatch> types =
        [
            .. Enumerable
                .Range(0, values.Length)
                .Where(x => expected[x].Kind != values[x].Kind)
                .Select(x => new ValueMismatch(
                    ValueMismatchKind.Type,
                    x,
                    null,
                    $"結果{x}の型が期待した{ValueCodec.GetTypeName(expected[x].Kind)}ではなく{ValueCodec.GetTypeName(values[x].Kind)}です。"
                )),
        ];

        // 順序や型が揃わない場合は、値を比較しない。
        return types.IsEmpty
            ? [.. values.SelectMany((x, i) => MatchValue(expected[i], x, i, state))]
            : types;
    }

    private static ValuePattern Parse(ExpectedValue value, string path)
    {
        return value switch
        {
            { Kind: WasmValueKind.I32 } => ParseNumber(value, 32, false, path),
            { Kind: WasmValueKind.I64 } => ParseNumber(value, 64, false, path),
            { Kind: WasmValueKind.F32 } => ParseNumber(value, 32, true, path),
            { Kind: WasmValueKind.F64 } => ParseNumber(value, 64, true, path),
            { Kind: WasmValueKind.V128, LaneType: { } laneType } => ParseVector(
                laneType,
                value.Lanes,
                path
            ),
            { Kind: WasmValueKind.FuncRef or WasmValueKind.ExternRef, Value: ValueCodec.NULL } =>
                new(value.Kind, 0, [], null),
            { Kind: WasmValueKind.ExternRef } => new(
                value.Kind,
                0,
                [],
                (uint)ValueCodec.ParseBits(value.Value, 32, path)
            ),
            _ => throw new ScriptFormatException(
                $"{path}の{value.Value}は固定形式にない{ValueCodec.GetTypeName(value.Kind)}の期待値です。"
            ),
        };
    }

    private static ValuePattern ParseNumber(
        ExpectedValue value,
        int width,
        bool floating,
        string path
    )
    {
        return new(value.Kind, width, [ParseBits(value.Value, width, floating, path)], null);
    }

    private static ValuePattern ParseVector(
        LaneType laneType,
        ImmutableArray<string> lanes,
        string path
    )
    {
        var width = ValueCodec.GetLaneWidth(laneType, lanes.Length, path);
        var floating = laneType is LaneType.F32 or LaneType.F64;
        return new(
            WasmValueKind.V128,
            width,
            [.. lanes.Select((x, i) => ParseBits(x, width, floating, $"{path}[{i}]"))],
            null
        );
    }

    private static BitsPattern ParseBits(string? text, int width, bool floating, string path)
    {
        // NaN patternは浮動小数点数の値とlaneだけが持ち、整数では10進数として検査して拒否する。
        return (floating, text) switch
        {
            (true, CANONICAL) => new(0, NanPattern.Canonical),
            (true, ARITHMETIC) => new(0, NanPattern.Arithmetic),
            _ => new(ValueCodec.ParseBits(text, width, path), null),
        };
    }

    private static IEnumerable<ValueMismatch> MatchValue(
        ValuePattern expected,
        WasmValue actual,
        int index,
        ScriptState state
    )
    {
        return actual.Kind switch
        {
            WasmValueKind.FuncRef => MatchReference(expected, actual.AsFuncRef(), index, state),
            WasmValueKind.ExternRef => MatchReference(expected, actual.AsExternRef(), index, state),
            WasmValueKind.V128 => expected.Bits.SelectMany(
                (x, i) =>
                    MatchBits(
                        x,
                        ValueCodec.GetLane(actual, expected.Width, i),
                        expected.Width,
                        index,
                        i
                    )
            ),
            _ => MatchBits(
                expected.Bits[0],
                ValueCodec.GetBits(actual),
                expected.Width,
                index,
                null
            ),
        };
    }

    private static IEnumerable<ValueMismatch> MatchBits(
        BitsPattern expected,
        ulong actual,
        int width,
        int index,
        int? lane
    )
    {
        return Matches(expected, actual, width)
            ? []
            :
            [
                CreateValueMismatch(
                    index,
                    lane,
                    expected.Nan switch
                    {
                        NanPattern.Canonical => CANONICAL,
                        NanPattern.Arithmetic => ARITHMETIC,
                        _ => $"0x{ValueCodec.ToHex(expected.Bits, width)}",
                    },
                    $"0x{ValueCodec.ToHex(actual, width)}"
                ),
            ];
    }

    private static bool Matches(BitsPattern expected, ulong actual, int width)
    {
        // NaN patternはf32・f64の幅でだけ解析されるため、幅から型の定数を選べる。
        var quietNan = width == 32 ? F32_QUIET_NAN : F64_QUIET_NAN;
        var sign = 1UL << (width - 1);
        return expected.Nan switch
        {
            NanPattern.Canonical => (actual & ~sign) == quietNan,
            NanPattern.Arithmetic => (actual & quietNan) == quietNan,
            _ => actual == expected.Bits,
        };
    }

    private static IEnumerable<ValueMismatch> MatchReference(
        ValuePattern expected,
        object? actual,
        int index,
        ScriptState state
    )
    {
        // 割り当てた番号は参照の同一性で引くため、番号の一致は同じ番号のホスト値と同じ参照であることを示す。
        var number = actual is null ? null : state.GetExternrefNumber(actual);
        var matched = expected.Externref is { } x ? number == x : actual is null;
        return matched
            ? []
            :
            [
                CreateValueMismatch(
                    index,
                    null,
                    expected.Externref is { } y ? $"番号{y}のexternref" : ValueCodec.NULL,
                    (actual, number) switch
                    {
                        (null, _) => ValueCodec.NULL,
                        (_, { } z) => $"番号{z}のexternref",
                        _ => $"割り当てていない非nullの{ValueCodec.GetTypeName(expected.Kind)}",
                    }
                ),
            ];
    }

    private static ValueMismatch CreateValueMismatch(
        int index,
        int? lane,
        string expected,
        string actual
    )
    {
        var position = lane is null ? $"結果{index}" : $"結果{index}のlane{lane}";
        return new(
            ValueMismatchKind.Value,
            index,
            lane,
            $"{position}の値が期待した{expected}ではなく{actual}です。"
        );
    }
}
