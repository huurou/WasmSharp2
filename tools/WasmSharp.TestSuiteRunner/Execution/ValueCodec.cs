using System.Collections.Immutable;
using System.Globalization;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// WABTの値の文字列からビット列と参照の同一性を保ったvalueを構築し、実値を保存用の表現へ写す
/// </summary>
/// <remarks>
/// 整数と浮動小数点数の文字列は、同じ幅の符号なし10進数によるビット列として読み、範囲外の数を切り詰めない。
/// 参照は型付きnullか、入力内で番号ごとに割り当てたexternrefだけを構築し、非nullのfuncrefを番号から推測しない。
/// </remarks>
internal static class ValueCodec
{
    /// <summary>
    /// WABTが参照のnullに使う値の文字列
    /// </summary>
    internal const string NULL = "null";

    /// <summary>
    /// invokeの引数を、JSONの順序・型・ビット列を保ったvalueの列へ変換する。
    /// </summary>
    /// <param name="values">JSONの記載順の引数</param>
    /// <param name="state">externrefの番号にホスト値を割り当てる入力の状態</param>
    /// <exception cref="ScriptFormatException">値の文字列が固定形式にない場合</exception>
    internal static ImmutableArray<WasmValue> CreateArguments(
        ImmutableArray<ArgumentValue> values,
        ScriptState state
    )
    {
        return [.. values.Select((x, i) => CreateArgument(x, $"action.args[{i}].value", state))];
    }

    /// <summary>
    /// 実値を、型と幅を固定したhexと入力内の参照tokenで記録する。
    /// </summary>
    /// <param name="value">記録するvalue</param>
    /// <param name="state">参照tokenとexternrefの番号を管理する入力の状態</param>
    internal static ValueRecord Record(WasmValue value, ScriptState state)
    {
        var record = new ValueRecord(GetTypeName(value.Kind));
        return value.Kind switch
        {
            WasmValueKind.I32 or WasmValueKind.F32 => record with
            {
                Bits = ToHex(GetBits(value), 32),
            },
            WasmValueKind.I64 or WasmValueKind.F64 => record with
            {
                Bits = ToHex(GetBits(value), 64),
            },
            WasmValueKind.V128 => record with
            {
                Low64 = ToHex(value.AsV128().Low64, 64),
                High64 = ToHex(value.AsV128().High64, 64),
            },
            WasmValueKind.FuncRef => RecordReference(record, value.AsFuncRef(), state),
            WasmValueKind.ExternRef => RecordReference(record, value.AsExternRef(), state),
            // exnrefは公開APIで参照先を取得できないため型だけを記録する。
            _ => record,
        };
    }

    /// <summary>
    /// 整数・浮動小数点数のvalueのビット列を、型の幅のまま下位に置いて返す。
    /// </summary>
    /// <param name="value">i32・i64・f32・f64のvalue</param>
    internal static ulong GetBits(WasmValue value)
    {
        return value.Kind switch
        {
            WasmValueKind.I32 => unchecked((uint)value.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
            WasmValueKind.F32 => value.AsF32Bits(),
            _ => value.AsF64Bits(),
        };
    }

    /// <summary>
    /// WABTの値型名を返す。
    /// </summary>
    internal static string GetTypeName(WasmValueKind kind)
    {
        return kind switch
        {
            WasmValueKind.I32 => "i32",
            WasmValueKind.I64 => "i64",
            WasmValueKind.F32 => "f32",
            WasmValueKind.F64 => "f64",
            WasmValueKind.V128 => "v128",
            WasmValueKind.FuncRef => "funcref",
            WasmValueKind.ExternRef => "externref",
            _ => "exnref",
        };
    }

    /// <summary>
    /// v128のlane数を確認し、1laneのビット幅を返す。
    /// </summary>
    /// <param name="laneType">lane型</param>
    /// <param name="count">JSONに記録されたlaneの個数</param>
    /// <param name="path">異常の報告に使うJSON上の位置</param>
    /// <exception cref="ScriptFormatException">lane数がlane型の個数と一致しない場合</exception>
    internal static int GetLaneWidth(LaneType laneType, int count, string path)
    {
        var width = laneType switch
        {
            LaneType.I8 => 8,
            LaneType.I16 => 16,
            LaneType.I32 or LaneType.F32 => 32,
            _ => 64,
        };
        return count == 128 / width
            ? width
            : throw new ScriptFormatException(
                $"{path}のlane数{count}は{width}ビットlaneの{128 / width}個と一致しません。"
            );
    }

    /// <summary>
    /// v128から指定したlaneのビット列を取り出す。lane0を下位ビットとする。
    /// </summary>
    /// <param name="value">v128のvalue</param>
    /// <param name="width">1laneのビット幅。8・16・32・64のいずれか</param>
    /// <param name="lane">0始まりのlane位置</param>
    internal static ulong GetLane(WasmValue value, int width, int lane)
    {
        // lane幅は64の約数のため、1つのlaneが下位と上位の64ビットにまたがらない。
        var (low64, high64) = value.AsV128();
        var offset = lane * width;
        return ((offset < 64 ? low64 : high64) >> (offset % 64)) & GetMask(width);
    }

    /// <summary>
    /// 指定した幅の符号なし10進数を、切り詰めずにビット列として読む。
    /// </summary>
    /// <param name="text">WABTが記録した値の文字列</param>
    /// <param name="width">ビット幅。8・16・32・64のいずれか</param>
    /// <param name="path">異常の報告に使うJSON上の位置</param>
    /// <exception cref="ScriptFormatException">ASCII数字以外や0以外の先頭の0を含むか、幅に収まらない場合</exception>
    internal static ulong ParseBits(string? text, int width, string path)
    {
        // 標準の解析は先頭の0や末尾のNULも受け付けるため、WABTの%u形式の文字だけに限定する。
        return
            text is ['0'] or [>= '1' and <= '9', ..]
            && text.All(char.IsAsciiDigit)
            && ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var bits)
            && (bits & ~GetMask(width)) == 0
            ? bits
            : throw new ScriptFormatException(
                $"{path}の{text}は{width}ビットの符号なし10進数ではありません。"
            );
    }

    /// <summary>
    /// ビット列を幅に応じた桁数の小文字hexで表す。
    /// </summary>
    /// <param name="bits">下位から幅の分だけを使うビット列</param>
    /// <param name="width">ビット幅。8・16・32・64のいずれか</param>
    internal static string ToHex(ulong bits, int width)
    {
        return bits.ToString($"x{width / 4}", CultureInfo.InvariantCulture);
    }

    private static WasmValue CreateArgument(ArgumentValue value, string path, ScriptState state)
    {
        return value switch
        {
            { Kind: WasmValueKind.I32 } => WasmValue.FromI32(
                unchecked((int)ParseBits(value.Value, 32, path))
            ),
            { Kind: WasmValueKind.I64 } => WasmValue.FromI64(
                unchecked((long)ParseBits(value.Value, 64, path))
            ),
            { Kind: WasmValueKind.F32 } => WasmValue.FromF32Bits(
                (uint)ParseBits(value.Value, 32, path)
            ),
            { Kind: WasmValueKind.F64 } => WasmValue.FromF64Bits(ParseBits(value.Value, 64, path)),
            { Kind: WasmValueKind.V128, LaneType: { } laneType } => CreateV128(
                laneType,
                value.Lanes,
                path
            ),
            { Kind: WasmValueKind.FuncRef, Value: NULL } => WasmValue.FromFuncRef(null),
            { Kind: WasmValueKind.ExternRef, Value: NULL } => WasmValue.FromExternRef(null),
            { Kind: WasmValueKind.ExternRef } => WasmValue.FromExternRef(
                state.GetExternref((uint)ParseBits(value.Value, 32, path))
            ),
            _ => throw new ScriptFormatException(
                $"{path}の{value.Value}は固定形式にない{GetTypeName(value.Kind)}の引数です。"
            ),
        };
    }

    private static WasmValue CreateV128(
        LaneType laneType,
        ImmutableArray<string> lanes,
        string path
    )
    {
        var width = GetLaneWidth(laneType, lanes.Length, path);
        ulong low64 = 0;
        ulong high64 = 0;
        for (var i = 0; i < lanes.Length; i++)
        {
            // メモリ上の並びを介さずシフトで配置し、CPUのendianに依存させない。
            var bits = ParseBits(lanes[i], width, $"{path}[{i}]");
            var offset = i * width;
            if (offset < 64)
            {
                low64 |= bits << offset;
            }
            else
            {
                high64 |= bits << (offset - 64);
            }
        }

        return WasmValue.FromV128(low64, high64);
    }

    private static ValueRecord RecordReference(
        ValueRecord record,
        object? reference,
        ScriptState state
    )
    {
        // 番号はランナーが割り当てたexternrefだけが持ち、funcrefや未知の参照ではnullになる。
        return reference is null
            ? record with
            {
                IsNull = true,
            }
            : record with
            {
                IsNull = false,
                Token = state.GetReferenceToken(reference),
                ExternrefNumber = state.GetExternrefNumber(reference),
            };
    }

    private static ulong GetMask(int width)
    {
        return width == 64 ? ulong.MaxValue : (1UL << width) - 1;
    }
}
