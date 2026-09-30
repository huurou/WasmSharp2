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
    /// <remarks>
    /// 途中の引数が不正な場合は部分配列を返さない。それまでに状態へ割り当てたexternrefの対応は保持する。
    /// </remarks>
    /// <param name="values">JSONの記載順の引数</param>
    /// <param name="state">externrefの番号にホスト値を割り当てる入力の状態</param>
    /// <returns>順序とビット列を保持する新しいvalueの配列 参照値は入力内の同じ番号の実体を共有する</returns>
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
    /// <remarks>
    /// Core 2.0の数値・v128・参照の記録を作る。参照先の取得APIがないexnrefでは型名だけを記録する。
    /// </remarks>
    /// <param name="value">記録するvalue</param>
    /// <param name="state">参照tokenとexternrefの番号を管理する入力の状態</param>
    /// <returns>数値の全ビット、v128の上下64ビット、参照のnull・token・元番号を型とともに保持する記録</returns>
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
    /// <returns>valueのビット列 32ビット型では上位32ビットを0にする</returns>
    /// <exception cref="InvalidOperationException">valueがi32・i64・f32・f64のいずれでもない場合</exception>
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
    /// <param name="kind">記録するWasmの値型</param>
    /// <returns>JSONへ記録する小文字の型名 Core 2.0の7値型以外はexnref</returns>
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
    /// <returns>lane型に応じた8・16・32・64のいずれかのビット幅</returns>
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
    /// <param name="width">1laneのビット幅 8・16・32・64のいずれか</param>
    /// <param name="lane">0始まりのlane位置 0以上、128をwidthで割った個数未満</param>
    /// <returns>指定laneのビット列 上位の未使用ビットは0</returns>
    /// <exception cref="InvalidOperationException">valueがv128ではない場合</exception>
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
    /// <param name="width">ビット幅 8・16・32・64のいずれか</param>
    /// <param name="path">異常の報告に使うJSON上の位置</param>
    /// <returns>指定幅に収まる符号なし整数として解釈したビット列</returns>
    /// <exception cref="ScriptFormatException">null・空文字列、ASCII数字以外や0以外の先頭の0を含む文字列、または幅に収まらない場合</exception>
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
    /// ビット列を幅に応じた最小桁数の小文字hexで表す。
    /// </summary>
    /// <param name="bits">表記するビット列 指定幅に収まる値を渡す</param>
    /// <param name="width">ビット幅 8・16・32・64のいずれか</param>
    /// <returns>0xを付けず、widthを4で割った桁数まで0を補った文字列 幅を超えるビットは切り詰めない</returns>
    internal static string ToHex(ulong bits, int width)
    {
        return bits.ToString($"x{width / 4}", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 1個の引数の文字列を検査し、数値の全ビットまたは参照の同一性を保ったvalueを作る。
    /// </summary>
    /// <param name="value">WABT形式の引数 NaN patternは受け付けない</param>
    /// <param name="path">不正な値を報告するJSON上の位置</param>
    /// <param name="state">非nullのexternrefを番号ごとに割り当てる入力の状態</param>
    /// <returns>型とビット列を保持する引数のvalue externrefは同じ番号のホスト値を参照する</returns>
    /// <exception cref="ScriptFormatException">値の文字列やlane数が不正、または非nullのfuncrefなど固定形式外の引数の場合</exception>
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

    /// <summary>
    /// lane0を下位ビットとし、laneの文字列からCPUのendianに依存せずv128を作る。
    /// </summary>
    /// <param name="laneType">各laneの型と幅</param>
    /// <param name="lanes">lane0から順の符号なし10進数によるビット列</param>
    /// <param name="path">lane数やlane値の異常を報告するJSON上の位置</param>
    /// <returns>すべてのlaneのビット列を順序どおりに配置したv128のvalue</returns>
    /// <exception cref="ScriptFormatException">lane数が型と合わないか、lane値が固定形式外または幅に収まらない場合</exception>
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

    /// <summary>
    /// 型の記録へ参照のnullと入力内の同一性を加えた新しい記録を作る。
    /// </summary>
    /// <param name="record">参照型の名前を持つ元の記録</param>
    /// <param name="reference">funcrefまたはexternrefの参照先 nullも許容する</param>
    /// <param name="state">参照tokenと、割り当てたexternrefの元番号を管理する入力の状態</param>
    /// <returns>nullならIsNullだけを設定し、非nullならtokenと取得できたexternref番号を追加した記録</returns>
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

    /// <summary>
    /// 指定幅の下位ビットだけを残すマスクを返す。
    /// </summary>
    /// <param name="width">ビット幅 8・16・32・64のいずれか</param>
    /// <returns>下位widthビットが1、残りが0の64ビット値</returns>
    private static ulong GetMask(int width)
    {
        return width == 64 ? ulong.MaxValue : (1UL << width) - 1;
    }
}
