using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// assert_returnの期待値と実際の結果を、個数・順序・型・ビット列・参照の同一性で比較する
/// </summary>
/// <remarks>
/// 個数と各位置の型を先に比較し、揃った場合だけ値を比較する。整数と具体的な浮動小数点数は全ビットで比較する。
/// NaN patternはscalarとv128の浮動小数点laneに同じ条件を使い、v128は期待lane型で分割してlaneごとに比較する。
/// 参照は公式ケースが指定したnull、参照の種類、番号付き参照の同一性を照合する。
/// </remarks>
internal static class ValueMatcher
{
    /// <summary>
    /// 符号を除いたcanonical NaNとの一致を期待する値表現
    /// </summary>
    private const string CANONICAL = "nan:canonical";

    /// <summary>
    /// 指数部がすべて1でquiet bitが1のNaNを期待する値表現
    /// </summary>
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
    /// <param name="specVersion">参照の照合規則を決める仕様版</param>
    /// <param name="signedIntegers">整数と整数laneで負の10進表記を受け付けるかどうか</param>
    /// <returns>結果位置ごとの候補配列 単独の期待値は1件、eitherは入れ子を展開した順に候補を保持する。</returns>
    /// <exception cref="ScriptFormatException">値の文字列が固定形式にない場合</exception>
    internal static ImmutableArray<ImmutableArray<ValuePattern>> Parse(
        ImmutableArray<ExpectedResult> expected,
        WasmSpecVersion specVersion = WasmSpecVersion.Core2,
        bool signedIntegers = false
    )
    {
        return [.. expected.Select((x, i) => ParseCandidates(x, $"expected[{i}]", specVersion, signedIntegers))];
    }

    /// <summary>
    /// 1つの結果位置で許容される値をすべて解析し、それぞれの値のJSON上の位置を診断に使用する。
    /// </summary>
    private static ImmutableArray<ValuePattern> ParseCandidates(
        ExpectedResult result,
        string path,
        WasmSpecVersion specVersion,
        bool signedIntegers
    )
    {
        if (result is ExpectedResult.Either either)
        {
            return
            [
                .. either.Candidates.SelectMany(
                    (x, i) => ParseCandidates(x, $"{path}.values[{i}]", specVersion, signedIntegers)
                ),
            ];
        }

        return [Parse((ExpectedValue)result, $"{path}.value", specVersion, signedIntegers)];
    }

    /// <summary>
    /// 解析済みの期待値と実際の結果を比較し、相違箇所を返す。
    /// </summary>
    /// <param name="expected">解析済みの期待値</param>
    /// <param name="actual">invokeの結果、またはgetの現在値を1個持つ結果</param>
    /// <param name="state">externrefの番号と割り当てたホスト値を管理する入力の状態</param>
    /// <returns>相違箇所 全て一致した場合は空</returns>
    internal static ImmutableArray<ValueMismatch> Match(
        ImmutableArray<ImmutableArray<ValuePattern>> expected,
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
                .Where(x => !expected[x].Any(y => MatchesType(y, values[x].Kind)))
                .Select(x => new ValueMismatch(
                    ValueMismatchKind.Type,
                    x,
                    null,
                    expected[x].Length == 1
                        ? $"結果{x}の型が期待した{expected[x][0].Type}ではなく{ValueCodec.GetTypeName(values[x].Kind)}です。"
                        : $"結果{x}の型{ValueCodec.GetTypeName(values[x].Kind)}と一致する候補がありません。"
                )),
        ];

        // 順序や型が揃わない場合は、値を比較しない。
        return types.IsEmpty ? [.. values.SelectMany((x, i) => MatchCandidates(expected[i], x, i, state))] : types;
    }

    /// <summary>
    /// 1つの候補が値全体と一致するとき、その結果位置は成立する。
    /// </summary>
    private static IEnumerable<ValueMismatch> MatchCandidates(
        ImmutableArray<ValuePattern> candidates,
        WasmValue actual,
        int index,
        ScriptState state
    )
    {
        if (candidates.Length == 1)
        {
            return MatchValue(candidates[0], actual, index, state);
        }

        return candidates.Any(x => MatchesType(x, actual.Kind) && !MatchValue(x, actual, index, state).Any())
            ? []
            : [new ValueMismatch(ValueMismatchKind.Value, index, null, $"結果{index}の値がどの候補とも一致しません。")];
    }

    /// <summary>
    /// 結果の種類が期待値の比較対象に含まれるか判定する。
    /// </summary>
    private static bool MatchesType(ValuePattern expected, WasmValueKind actual)
    {
        return expected.Reference is null
            ? expected.Type == ValueCodec.GetTypeName(actual)
            : actual is WasmValueKind.FuncRef or WasmValueKind.ExternRef;
    }

    /// <summary>
    /// 1個の期待値を検査し、数値・v128・参照の型に応じた比較条件へ変換する。
    /// </summary>
    /// <param name="value">値の文字列を加工せず保持した期待値</param>
    /// <param name="path">値の異常を報告するJSON上の位置</param>
    /// <param name="specVersion">参照の照合規則を決める仕様版</param>
    /// <param name="signedIntegers">整数と整数laneで負の10進表記を受け付けるかどうか</param>
    /// <returns>数値のビット列やNaN条件、または参照の比較条件</returns>
    /// <exception cref="ScriptFormatException">値やlane数が固定形式に合わない場合</exception>
    private static ValuePattern Parse(ExpectedValue value, string path, WasmSpecVersion specVersion, bool signedIntegers)
    {
        if (specVersion == WasmSpecVersion.Core3 && ExpectedValue.GetCore3ReferenceMatches(value.Type) is { } matches)
        {
            var comparison = value.Value switch
            {
                null => matches.Omitted,
                ValueCodec.NULL => matches.ExplicitNull,
                _ => matches.Numbered,
            } ?? throw new ScriptFormatException($"{path}の{value.Value}は固定形式にない{value.Type}の期待値です。");
            uint? number =
                comparison == Core3ReferenceMatch.Identity ? (uint)ValueCodec.ParseBits(value.Value, 32, path) : null;
            return new ValuePattern(value.Type, 0, [], number) { Reference = comparison };
        }

        return value switch
        {
            { Type: "i32" } => ParseNumber(value, 32, false, path, signedIntegers),
            { Type: "i64" } => ParseNumber(value, 64, false, path, signedIntegers),
            { Type: "f32" } => ParseNumber(value, 32, true, path, signedIntegers),
            { Type: "f64" } => ParseNumber(value, 64, true, path, signedIntegers),
            { Type: "v128", LaneType: { } laneType } => ParseVector(laneType, value.Lanes, path, signedIntegers),
            { Type: "funcref" or "externref", Value: ValueCodec.NULL } => new(value.Type, 0, [], null),
            { Type: "externref" } => new(value.Type, 0, [], (uint)ValueCodec.ParseBits(value.Value, 32, path)),
            _ => throw new ScriptFormatException($"{path}の{value.Value}は固定形式にない{value.Type}の期待値です。"),
        };
    }

    /// <summary>
    /// scalarの数値を1個のビット列またはNaN条件として解析する。
    /// </summary>
    /// <param name="value">i32・i64・f32・f64の期待値</param>
    /// <param name="width">値型に対応する32または64のビット幅</param>
    /// <param name="floating">浮動小数点型としてNaN patternを許容するかどうか</param>
    /// <param name="path">値の異常を報告するJSON上の位置</param>
    /// <param name="signedIntegers">整数で負の10進表記を許容するかどうか</param>
    /// <returns>元の値型と幅を持つ1個の数値の比較条件</returns>
    /// <exception cref="ScriptFormatException">文字列が許容するNaN patternでも変換器と型が許容する10進表記でもない場合</exception>
    private static ValuePattern ParseNumber(ExpectedValue value, int width, bool floating, string path, bool signedIntegers)
    {
        return new(value.Type, width, [ParseBits(value.Value, width, floating, path, signedIntegers)], null);
    }

    /// <summary>
    /// v128のlane数を検査し、lane0から順にビット列またはNaN条件を解析する。
    /// </summary>
    /// <param name="laneType">比較に使うlaneの型 浮動小数点laneだけNaN patternを許容する</param>
    /// <param name="lanes">lane0から順の値の文字列</param>
    /// <param name="path">lane数やlane値の異常を報告するJSON上の位置</param>
    /// <param name="signedIntegers">整数で負の10進表記を許容するかどうか</param>
    /// <returns>laneの幅と順序を持つv128の比較条件</returns>
    /// <exception cref="ScriptFormatException">lane数が型と合わないか、lane値が許容するNaN patternでも変換器と型が許容する10進表記でもない場合</exception>
    private static ValuePattern ParseVector(
        LaneType laneType,
        ImmutableArray<string> lanes,
        string path,
        bool signedIntegers
    )
    {
        var width = ValueCodec.GetLaneWidth(laneType, lanes.Length, path);
        var floating = laneType is LaneType.F32 or LaneType.F64;
        return new(
            "v128",
            width,
            [.. lanes.Select((x, i) => ParseBits(x, width, floating, $"{path}[{i}]", signedIntegers))],
            null
        );
    }

    /// <summary>
    /// 浮動小数点数のNaN patternを区別し、それ以外の文字列を具体的なビット列として解析する。
    /// </summary>
    /// <param name="text">scalarまたは1laneの値の文字列</param>
    /// <param name="width">値のビット幅 8・16・32・64のいずれか</param>
    /// <param name="floating">NaN patternを許容する浮動小数点数かどうか</param>
    /// <param name="path">値の異常を報告するJSON上の位置</param>
    /// <param name="signedIntegers">整数で負の10進表記を許容するかどうか</param>
    /// <returns>canonical・arithmeticのNaN条件または指定幅に収まる具体的なビット列</returns>
    /// <exception cref="ScriptFormatException">文字列が許容するNaN patternでも変換器と型が許容する10進表記でもない場合</exception>
    private static BitsPattern ParseBits(string? text, int width, bool floating, string path, bool signedIntegers)
    {
        // NaN patternは浮動小数点数の値とlaneだけが持ち、整数では10進数として検査して拒否する。
        return (floating, text) switch
        {
            (true, CANONICAL) => new(0, NanPattern.Canonical),
            (true, ARITHMETIC) => new(0, NanPattern.Arithmetic),
            _ => new(ValueCodec.ParseBits(text, width, path, signedIntegers && !floating), null),
        };
    }

    /// <summary>
    /// 1個の結果を候補と比較し、数値・lane・参照の相違箇所を返す。
    /// </summary>
    /// <param name="expected"><see cref="MatchesType"/>で比較対象と判定された候補 Core 3.0の参照では実際の値と型名が異なる場合もある。</param>
    /// <param name="actual">比較する1個の結果</param>
    /// <param name="index">結果の0始まり位置</param>
    /// <param name="state">参照の同一性でexternref番号を解決する入力の状態</param>
    /// <returns>値の相違 v128ではlane順に返し、すべて一致した場合は空</returns>
    private static IEnumerable<ValueMismatch> MatchValue(
        ValuePattern expected,
        WasmValue actual,
        int index,
        ScriptState state
    )
    {
        return actual.Kind switch
        {
            WasmValueKind.FuncRef => MatchReference(expected, actual.Kind, actual.AsFuncRef(), index, state),
            WasmValueKind.ExternRef => MatchReference(expected, actual.Kind, actual.AsExternRef(), index, state),
            WasmValueKind.V128 => expected.Bits.SelectMany(
                (x, i) => MatchBits(x, ValueCodec.GetLane(actual, expected.Width, i), expected.Width, index, i)
            ),
            _ => MatchBits(expected.Bits[0], ValueCodec.GetBits(actual), expected.Width, index, null),
        };
    }

    /// <summary>
    /// scalarまたは1laneのビット列を比較し、不一致なら期待と実際を記録する。
    /// </summary>
    /// <param name="expected">指定幅の具体的なビット列またはNaN条件</param>
    /// <param name="actual">指定幅に収まる実際のビット列</param>
    /// <param name="width">比較するビット幅 8・16・32・64のいずれか</param>
    /// <param name="index">結果の0始まり位置</param>
    /// <param name="lane">v128の0始まりlane位置 scalarの場合はnull</param>
    /// <returns>一致した場合は空 不一致の場合はNaN条件またはhexと実際のhexを示す1件の相違</returns>
    private static IEnumerable<ValueMismatch> MatchBits(BitsPattern expected, ulong actual, int width, int index, int? lane)
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

    /// <summary>
    /// 具体値の全ビット、またはcanonical・arithmeticのNaN条件との一致を判定する。
    /// </summary>
    /// <param name="expected">具体的なビット列またはNaN条件</param>
    /// <param name="actual">指定幅に収まる実際のビット列</param>
    /// <param name="width">比較するビット幅 NaN条件では32または64、それ以外では8・16・32・64</param>
    /// <returns>期待条件を満たす場合はtrue NaN条件では符号を問わずquiet bitを含む型ごとの規則を使う</returns>
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

    /// <summary>
    /// スクリプトの参照条件をランタイムが返した参照と比較する。
    /// </summary>
    /// <param name="expected">解析済みの参照の比較条件</param>
    /// <param name="actualKind">実際の参照の値型</param>
    /// <param name="actual">実際の参照先 nullも許容する</param>
    /// <param name="index">結果の0始まり位置</param>
    /// <param name="state">割り当てたホスト値から参照の同一性で元のexternref番号を引く入力の状態</param>
    /// <returns>一致した場合は空 不一致の場合はnull・番号・未知の非null参照を区別した1件の相違</returns>
    private static IEnumerable<ValueMismatch> MatchReference(
        ValuePattern expected,
        WasmValueKind actualKind,
        object? actual,
        int index,
        ScriptState state
    )
    {
        // 割り当てた番号は参照の同一性で引くため、番号の一致は同じ番号のホスト値と同じ参照であることを示す。
        var number = actual is null ? null : state.GetExternrefNumber(actual);
        var isFunction = actualKind == WasmValueKind.FuncRef && actual is not null;
        var matched = expected.Reference switch
        {
            // Core 3.0では型を付けたref.nullも参照先がnullなら一致する。
            Core3ReferenceMatch.Null => actual is null,
            // 固定版の参照実装では(ref.extern)は全ての参照に、(ref.any)は関数実体を除く参照に一致する。
            Core3ReferenceMatch.HeapType => expected.Type switch
            {
                "externref" => true,
                "anyref" => !isFunction,
                "funcref" => isFunction,
                _ => false,
            },
            // ref.hostと、それを外部参照に包んだref.externは同じ番号でも異なる。
            Core3ReferenceMatch.Identity => expected.Type == "externref"
                && actualKind == WasmValueKind.ExternRef
                && number == expected.ReferenceNumber,
            _ => expected.ReferenceNumber is { } x ? number == x : actual is null,
        };
        return matched
            ? []
            :
            [
                CreateValueMismatch(
                    index,
                    null,
                    expected switch
                    {
                        { ReferenceNumber: { } y } => $"番号{y}の{expected.Type}",
                        { Reference: Core3ReferenceMatch.HeapType } => expected.Type,
                        _ => ValueCodec.NULL,
                    },
                    (actual, number) switch
                    {
                        (null, _) => ValueCodec.NULL,
                        (_, { } z) => $"番号{z}のexternref",
                        _ => $"割り当てていない非nullの{ValueCodec.GetTypeName(actualKind)}",
                    }
                ),
            ];
    }

    /// <summary>
    /// 結果位置と任意のlane位置を付け、期待と実際を示す値の相違を作る。
    /// </summary>
    /// <param name="index">結果の0始まり位置</param>
    /// <param name="lane">v128の0始まりlane位置 scalarと参照ではnull</param>
    /// <param name="expected">診断に表示する期待条件</param>
    /// <param name="actual">診断に表示する実際の値</param>
    /// <returns>位置と双方の表現を含むValue分類の相違</returns>
    private static ValueMismatch CreateValueMismatch(int index, int? lane, string expected, string actual)
    {
        var position = lane is null ? $"結果{index}" : $"結果{index}のlane{lane}";
        return new(ValueMismatchKind.Value, index, lane, $"{position}の値が期待した{expected}ではなく{actual}です。");
    }
}
