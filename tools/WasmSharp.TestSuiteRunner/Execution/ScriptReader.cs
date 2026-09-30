using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 列挙済みJSONのcommandを、固定WABT形式の種類別モデルへ読み取る
/// </summary>
/// <remarks>
/// 種類・値型・lane型・module形式の名前と、項目の過不足・重複を固定形式に照らして確認する。
/// 値の文字列は加工せず保持し、ビット列・参照・NaN patternとしての解釈は引数構築と結果の比較で行う。
/// </remarks>
internal static class ScriptReader
{
    /// <summary>
    /// commandの読み取り異常を診断へ記録する操作名
    /// </summary>
    private const string OPERATION = "read_command";

    /// <summary>
    /// command・action・値の種類を示すJSONの項目名
    /// </summary>
    private const string TYPE = "type";

    /// <summary>
    /// 元入力の行番号を示すJSONの項目名
    /// </summary>
    private const string LINE = "line";

    /// <summary>
    /// 通常moduleの識別子またはregisterの対象識別子を示すJSONの項目名
    /// </summary>
    private const string NAME = "name";

    /// <summary>
    /// module素材のファイル名を示すJSONの項目名
    /// </summary>
    private const string FILENAME = "filename";

    /// <summary>
    /// module素材のbinary・textの形式を示すJSONの項目名
    /// </summary>
    private const string MODULE_TYPE = "module_type";

    /// <summary>
    /// registerの登録名を示すJSONの項目名
    /// </summary>
    private const string AS = "as";

    /// <summary>
    /// commandが行うinvoke・getを示すJSONの項目名
    /// </summary>
    private const string ACTION = "action";

    /// <summary>
    /// 否定assertionの期待診断を示すJSONの項目名
    /// </summary>
    private const string TEXT = "text";

    /// <summary>
    /// 値付き期待値または型だけの結果宣言を示すJSONの項目名
    /// </summary>
    private const string EXPECTED = "expected";

    /// <summary>
    /// actionの対象moduleの識別子を示すJSONの項目名
    /// </summary>
    private const string MODULE = "module";

    /// <summary>
    /// actionの対象export名を示すJSONの項目名
    /// </summary>
    private const string FIELD = "field";

    /// <summary>
    /// invokeの順序付き引数を示すJSONの項目名
    /// </summary>
    private const string ARGS = "args";

    /// <summary>
    /// scalar・参照の文字列またはv128のlane配列を示すJSONの項目名
    /// </summary>
    private const string VALUE = "value";

    /// <summary>
    /// v128のlane型を示すJSONの項目名
    /// </summary>
    private const string LANE_TYPE = "lane_type";

    /// <summary>
    /// 列挙済みの全commandを順序どおりに読み取る。読み取れないcommandも同じ位置に1件として残す。
    /// </summary>
    /// <param name="document">照合済みのJSON</param>
    /// <param name="inputPath">test/core基準の入力相対path</param>
    /// <returns>列挙済みcommandのindexと順序を保つ読み取り結果 個々の構造異常はInvalidCommandとして残す</returns>
    internal static ScriptReadResult Read(ScriptDocument document, string inputPath)
    {
        return new(
            inputPath,
            [.. document.Commands.Select(x => ReadCommand(document.GetJson(x.Index), x))],
            document.EnumerationComplete
        );
    }

    /// <summary>
    /// 境界が確定した1件のJSONを読み、固定形式に合わない場合も元の位置と内容を残す。
    /// </summary>
    /// <param name="json">列挙時に構文と境界を確認したcommandのUTF-8バイト列</param>
    /// <param name="command">列挙時に確定したindexと、取得できた行番号・種類</param>
    /// <returns>型付きcommand 固定形式の種類・構造に合わない場合は元JSONと診断を持つInvalidCommand</returns>
    private static ScriptCommand ReadCommand(ReadOnlyMemory<byte> json, EnumeratedCommand command)
    {
        // 境界は列挙時に確定済みのため再読取は失敗しない。
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        try
        {
            return ReadCommand(root, command.Index);
        }
        catch (ScriptFormatException ex)
        {
            // 列挙時に取得できた行と種類を使い、更新対象の名前は一意に読める場合だけ残す。
            return new InvalidCommand(
                command.Index,
                command.Line,
                command.Type,
                new(OPERATION, ex.Message) { SourceJson = Encoding.UTF8.GetString(json.Span) }
            )
            {
                Name = command.Type == ScriptCommand.MODULE ? GetSingleString(root, NAME) : null,
                As = command.Type == ScriptCommand.REGISTER ? GetSingleString(root, AS) : null,
            };
        }
    }

    /// <summary>
    /// commandの項目の重複と種類を確認し、固定した10種類のモデルへ読み取る。
    /// </summary>
    /// <param name="element">1件のcommandを表すJSON要素</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <returns>種類ごとの必須項目を持つcommand</returns>
    /// <exception cref="ScriptFormatException">objectではない、項目が重複する、種類が未知、または種類ごとの構造に合わない場合</exception>
    private static ScriptCommand ReadCommand(JsonElement element, int index)
    {
        var properties = ReadObject(element, "");
        var type = GetString(properties, "", TYPE);
        return type switch
        {
            ScriptCommand.MODULE => ReadModule(properties, index),
            ScriptCommand.REGISTER => ReadRegister(properties, index),
            ScriptCommand.ACTION => ReadActionCommand(properties, index),
            ScriptCommand.ASSERT_RETURN => ReadAssertReturn(properties, index),
            ScriptCommand.ASSERT_TRAP => ReadActionAssertion(
                properties,
                index,
                static (i, l, a, t, r) => new AssertTrapCommand(i, l, a, t, r)
            ),
            ScriptCommand.ASSERT_EXHAUSTION => ReadActionAssertion(
                properties,
                index,
                static (i, l, a, t, r) => new AssertExhaustionCommand(i, l, a, t, r)
            ),
            ScriptCommand.ASSERT_MALFORMED => ReadModuleAssertion(
                properties,
                index,
                static (i, l, f, t, m) => new AssertMalformedCommand(i, l, f, t, m)
            ),
            ScriptCommand.ASSERT_INVALID => ReadModuleAssertion(
                properties,
                index,
                static (i, l, f, t, m) => new AssertInvalidCommand(i, l, f, t, m)
            ),
            ScriptCommand.ASSERT_UNLINKABLE => ReadModuleAssertion(
                properties,
                index,
                static (i, l, f, t, m) => new AssertUnlinkableCommand(i, l, f, t, m)
            ),
            ScriptCommand.ASSERT_UNINSTANTIABLE => ReadModuleAssertion(
                properties,
                index,
                static (i, l, f, t, m) => new AssertUninstantiableCommand(i, l, f, t, m)
            ),
            _ => throw Unknown(TYPE, type),
        };
    }

    /// <summary>
    /// 通常moduleの行番号・識別子・素材名を読み、形式の省略はbinaryとする。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <returns>module識別子が省略されていればNameがnullの通常module</returns>
    /// <exception cref="ScriptFormatException">項目の過不足、型、行番号、module形式が固定形式に合わない場合</exception>
    private static ModuleCommand ReadModule(Dictionary<string, JsonElement> properties, int index)
    {
        RejectUnknownNames(properties, "", TYPE, LINE, NAME, FILENAME, MODULE_TYPE);
        return new(
            index,
            ReadLine(properties),
            GetOptionalString(properties, "", NAME),
            GetString(properties, "", FILENAME),
            GetOptionalString(properties, "", MODULE_TYPE) is { } moduleType
                ? ParseModuleType(moduleType)
                : ScriptModuleType.Binary
        );
    }

    /// <summary>
    /// registerの行番号・任意の対象module識別子・必須の登録名を読み取る。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <returns>module識別子が省略されていれば直近moduleを対象とするregister</returns>
    /// <exception cref="ScriptFormatException">項目の過不足、型、行番号が固定形式に合わない場合</exception>
    private static RegisterCommand ReadRegister(
        Dictionary<string, JsonElement> properties,
        int index
    )
    {
        RejectUnknownNames(properties, "", TYPE, LINE, NAME, AS);
        return new(
            index,
            ReadLine(properties),
            GetOptionalString(properties, "", NAME),
            GetString(properties, "", AS)
        );
    }

    /// <summary>
    /// 単独actionと型だけの結果宣言を、値の一致を求めるassertionにせず読み取る。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <returns>invokeまたはgetと型だけの結果宣言を持つ単独action</returns>
    /// <exception cref="ScriptFormatException">command・action・結果宣言の項目や型が固定形式に合わない場合</exception>
    private static ActionCommand ReadActionCommand(
        Dictionary<string, JsonElement> properties,
        int index
    )
    {
        RejectUnknownNames(properties, "", TYPE, LINE, ACTION, EXPECTED);
        return new(
            index,
            ReadLine(properties),
            ReadAction(properties),
            ReadResultTypes(properties)
        );
    }

    /// <summary>
    /// assert_returnのactionと値付き期待値をJSONの順序どおりに読み取る。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <returns>値の文字列を加工せず保持するassert_return</returns>
    /// <exception cref="ScriptFormatException">command・action・期待値の項目や型が固定形式に合わない場合</exception>
    private static AssertReturnCommand ReadAssertReturn(
        Dictionary<string, JsonElement> properties,
        int index
    )
    {
        RejectUnknownNames(properties, "", TYPE, LINE, ACTION, EXPECTED);
        return new(
            index,
            ReadLine(properties),
            ReadAction(properties),
            ReadValues(
                GetArray(properties, "", EXPECTED),
                EXPECTED,
                static (k, v, l, x) => new ExpectedValue(k, v, l, x)
            )
        );
    }

    /// <summary>
    /// actionでのtrapまたはexhaustionを期待するassertionの共通項目を読み取る。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <param name="create">index・行番号・action・期待診断・型だけの結果宣言から種類ごとのcommandを作る関数</param>
    /// <returns>期待診断を加工せず保持するassert_trapまたはassert_exhaustion</returns>
    /// <exception cref="ScriptFormatException">command・action・結果宣言の項目や型が固定形式に合わない場合</exception>
    private static ScriptCommand ReadActionAssertion(
        Dictionary<string, JsonElement> properties,
        int index,
        Func<int, int, ScriptAction, string, ImmutableArray<WasmValueKind>, ScriptCommand> create
    )
    {
        RejectUnknownNames(properties, "", TYPE, LINE, ACTION, TEXT, EXPECTED);
        return create(
            index,
            ReadLine(properties),
            ReadAction(properties),
            GetString(properties, "", TEXT),
            ReadResultTypes(properties)
        );
    }

    /// <summary>
    /// module処理での失敗を期待するassertionの素材名・期待診断・必須の形式を読み取る。
    /// </summary>
    /// <param name="properties">項目名の重複を検査済みのcommand</param>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <param name="create">index・行番号・素材名・期待診断・素材形式から種類ごとのcommandを作る関数</param>
    /// <returns>期待診断を加工せず保持するmoduleの否定assertion</returns>
    /// <exception cref="ScriptFormatException">項目の過不足、型、行番号、module形式が固定形式に合わない場合</exception>
    private static ScriptCommand ReadModuleAssertion(
        Dictionary<string, JsonElement> properties,
        int index,
        Func<int, int, string, string, ScriptModuleType, ScriptCommand> create
    )
    {
        RejectUnknownNames(properties, "", TYPE, LINE, FILENAME, TEXT, MODULE_TYPE);
        return create(
            index,
            ReadLine(properties),
            GetString(properties, "", FILENAME),
            GetString(properties, "", TEXT),
            ParseModuleType(GetString(properties, "", MODULE_TYPE))
        );
    }

    /// <summary>
    /// commandのactionを、任意の対象module識別子と必須のexport名を持つinvokeまたはgetへ読み取る。
    /// </summary>
    /// <param name="command">action項目を持つcommand</param>
    /// <returns>invokeでは引数を順序どおり保持し、getでは引数を持たない操作</returns>
    /// <exception cref="ScriptFormatException">actionの種類、項目の過不足・重複、型、引数の構造が固定形式に合わない場合</exception>
    private static ScriptAction ReadAction(Dictionary<string, JsonElement> command)
    {
        var properties = ReadObject(GetRequired(command, "", ACTION), ACTION);
        var type = GetString(properties, ACTION, TYPE);
        switch (type)
        {
            case "invoke":
                RejectUnknownNames(properties, ACTION, TYPE, MODULE, FIELD, ARGS);
                return new InvokeAction(
                    GetOptionalString(properties, ACTION, MODULE),
                    GetString(properties, ACTION, FIELD),
                    ReadValues(
                        GetArray(properties, ACTION, ARGS),
                        Join(ACTION, ARGS),
                        static (k, v, l, x) => new ArgumentValue(k, v, l, x)
                    )
                );
            case "get":
                RejectUnknownNames(properties, ACTION, TYPE, MODULE, FIELD);
                return new GetAction(
                    GetOptionalString(properties, ACTION, MODULE),
                    GetString(properties, ACTION, FIELD)
                );
            default:
                throw Unknown(Join(ACTION, TYPE), type);
        }
    }

    /// <summary>
    /// 引数または値付き期待値の配列を順序どおりに読み、指定したモデルの新しい配列を作る。
    /// </summary>
    /// <typeparam name="T">引数または値付き期待値のモデルの型</typeparam>
    /// <param name="array">配列であることを確認済みのJSON要素</param>
    /// <param name="path">配列のJSON上の位置 要素indexを加えて異常を報告する</param>
    /// <param name="create">値型・scalar文字列・lane型・lane文字列配列からモデルを作る関数</param>
    /// <returns>元JSONの順序を保持するモデルの配列 空配列なら空</returns>
    /// <exception cref="ScriptFormatException">配列要素の項目や型が固定形式に合わない場合</exception>
    private static ImmutableArray<T> ReadValues<T>(
        JsonElement array,
        string path,
        Func<WasmValueKind, string?, LaneType?, ImmutableArray<string>, T> create
    )
    {
        return [.. array.EnumerateArray().Select((x, i) => ReadValue(x, $"{path}[{i}]", create))];
    }

    /// <summary>
    /// 1個の値の構造と型を読み、scalar・参照の文字列またはv128のlane列を加工せず保持する。
    /// </summary>
    /// <typeparam name="T">引数または値付き期待値のモデルの型</typeparam>
    /// <param name="element">1個の値を表すJSON要素</param>
    /// <param name="path">値のJSON上の位置</param>
    /// <param name="create">値型・scalar文字列・lane型・lane文字列配列からモデルを作る関数</param>
    /// <returns>種類に応じた文字列表現を持つモデル ビット列の解釈とlane数の検査は後続処理へ残す</returns>
    /// <exception cref="ScriptFormatException">項目の過不足・重複、値型、lane型、文字列や配列の構造が固定形式に合わない場合</exception>
    private static T ReadValue<T>(
        JsonElement element,
        string path,
        Func<WasmValueKind, string?, LaneType?, ImmutableArray<string>, T> create
    )
    {
        var properties = ReadObject(element, path);
        var kind = ParseValueKind(GetString(properties, path, TYPE), Join(path, TYPE));
        if (kind != WasmValueKind.V128)
        {
            RejectUnknownNames(properties, path, TYPE, VALUE);
            return create(kind, GetString(properties, path, VALUE), null, []);
        }

        RejectUnknownNames(properties, path, TYPE, LANE_TYPE, VALUE);
        var laneType = ParseLaneType(GetString(properties, path, LANE_TYPE), Join(path, LANE_TYPE));
        var lanesPath = Join(path, VALUE);
        return create(
            kind,
            null,
            laneType,
            [
                .. GetArray(properties, path, VALUE)
                    .EnumerateArray()
                    .Select((x, i) => ToString(x, $"{lanesPath}[{i}]")),
            ]
        );
    }

    /// <summary>
    /// 型だけのexpectedを読み、値付き項目を許容せず結果型を順序どおりに保持する。
    /// </summary>
    /// <param name="command">型だけのexpected配列を持つcommand</param>
    /// <returns>JSONの記載順の結果型 新しい配列として保持し、空の宣言なら空</returns>
    /// <exception cref="ScriptFormatException">expectedがない、配列でない、または要素が既知のtypeだけを持つobjectではない場合</exception>
    private static ImmutableArray<WasmValueKind> ReadResultTypes(
        Dictionary<string, JsonElement> command
    )
    {
        return
        [
            .. GetArray(command, "", EXPECTED)
                .EnumerateArray()
                .Select(
                    (x, i) =>
                    {
                        // 型だけの結果宣言は値を持たない。
                        var path = $"{EXPECTED}[{i}]";
                        var properties = ReadObject(x, path);
                        RejectUnknownNames(properties, path, TYPE);
                        return ParseValueKind(GetString(properties, path, TYPE), Join(path, TYPE));
                    }
                ),
        ];
    }

    /// <summary>
    /// 元入力の行番号を1以上の32ビット整数として読み取る。
    /// </summary>
    /// <param name="properties">line項目を持つcommand</param>
    /// <returns>元入力の1始まり行番号</returns>
    /// <exception cref="ScriptFormatException">lineがないか、1以上の32ビット整数ではない場合</exception>
    private static int ReadLine(Dictionary<string, JsonElement> properties)
    {
        return
            GetRequired(properties, "", LINE) is { ValueKind: JsonValueKind.Number } line
            && line.TryGetInt32(out var value)
            && value > 0
            ? value
            : throw new ScriptFormatException($"{LINE}が1以上の整数ではありません。");
    }

    /// <summary>
    /// 固定Core 2.0形式の7値型の名前をWasmの値型へ対応付ける。
    /// </summary>
    /// <param name="type">加工していないJSONの値型名</param>
    /// <param name="path">未知の型名を報告するJSON上の位置</param>
    /// <returns>名前が大文字小文字まで一致するWasmの値型</returns>
    /// <exception cref="ScriptFormatException">値型名がi32・i64・f32・f64・v128・funcref・externrefのいずれでもない場合</exception>
    private static WasmValueKind ParseValueKind(string type, string path)
    {
        return type switch
        {
            "i32" => WasmValueKind.I32,
            "i64" => WasmValueKind.I64,
            "f32" => WasmValueKind.F32,
            "f64" => WasmValueKind.F64,
            "v128" => WasmValueKind.V128,
            "funcref" => WasmValueKind.FuncRef,
            "externref" => WasmValueKind.ExternRef,
            _ => throw Unknown(path, type),
        };
    }

    /// <summary>
    /// v128の固定した6種類のlane型の名前を対応付ける。
    /// </summary>
    /// <param name="laneType">加工していないJSONのlane型名</param>
    /// <param name="path">未知のlane型名を報告するJSON上の位置</param>
    /// <returns>名前が大文字小文字まで一致するlane型</returns>
    /// <exception cref="ScriptFormatException">lane型名がi8・i16・i32・i64・f32・f64のいずれでもない場合</exception>
    private static LaneType ParseLaneType(string laneType, string path)
    {
        return laneType switch
        {
            "i8" => LaneType.I8,
            "i16" => LaneType.I16,
            "i32" => LaneType.I32,
            "i64" => LaneType.I64,
            "f32" => LaneType.F32,
            "f64" => LaneType.F64,
            _ => throw Unknown(path, laneType),
        };
    }

    /// <summary>
    /// module素材のbinary・textの形式名を対応付ける。
    /// </summary>
    /// <param name="moduleType">加工していないJSONのmodule_type</param>
    /// <returns>名前が大文字小文字まで一致する素材形式</returns>
    /// <exception cref="ScriptFormatException">形式名がbinaryまたはtextではない場合</exception>
    private static ScriptModuleType ParseModuleType(string moduleType)
    {
        return moduleType switch
        {
            "binary" => ScriptModuleType.Binary,
            "text" => ScriptModuleType.Text,
            _ => throw Unknown(MODULE_TYPE, moduleType),
        };
    }

    /// <summary>
    /// JSONのobjectを項目名の重複を許さないOrdinalの辞書へ読み取る。
    /// </summary>
    /// <param name="element">読み取るJSON要素</param>
    /// <param name="path">objectのJSON上の位置 ルートのcommandでは空文字列</param>
    /// <returns>元のJsonDocumentの要素を参照する新しい項目辞書</returns>
    /// <exception cref="ScriptFormatException">objectではない、項目名を文字列として取得できない、または同名項目が重複する場合</exception>
    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, string path)
    {
        var target = path.Length == 0 ? "command" : path;
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ScriptFormatException($"{target}がobjectではありません。");
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            string name;
            try
            {
                name = property.Name;
            }
            catch (InvalidOperationException)
            {
                // 対になるサロゲートを欠くエスケープ等を含む名前は、固定形式のどの項目名とも一致しない。
                throw new ScriptFormatException(
                    $"{target}に文字列として取得できない項目名があります。"
                );
            }

            if (!properties.TryAdd(name, property.Value))
            {
                // どちらの値を採用するか決められないため、構造不正とする。
                throw new ScriptFormatException($"{Join(path, name)}が重複しています。");
            }
        }

        return properties;
    }

    /// <summary>
    /// 固定形式で許容する名前以外の項目を拒否する。
    /// </summary>
    /// <param name="properties">検査する項目辞書</param>
    /// <param name="path">objectのJSON上の位置 ルートのcommandでは空文字列</param>
    /// <param name="names">許容する項目名の集合 大文字小文字を区別する</param>
    /// <exception cref="ScriptFormatException">許容する名前に含まれない項目がある場合</exception>
    private static void RejectUnknownNames(
        Dictionary<string, JsonElement> properties,
        string path,
        params ReadOnlySpan<string> names
    )
    {
        foreach (var name in properties.Keys)
        {
            if (!names.Contains(name))
            {
                throw new ScriptFormatException($"固定形式にない{Join(path, name)}があります。");
            }
        }
    }

    /// <summary>
    /// 必須項目を取得し、欠落していれば位置付きの読み取り異常にする。
    /// </summary>
    /// <param name="properties">取得元の項目辞書</param>
    /// <param name="path">objectのJSON上の位置</param>
    /// <param name="name">必須の項目名</param>
    /// <returns>指定した項目のJSON要素 値の種類は検査しない</returns>
    /// <exception cref="ScriptFormatException">指定した項目がない場合</exception>
    private static JsonElement GetRequired(
        Dictionary<string, JsonElement> properties,
        string path,
        string name
    )
    {
        return properties.TryGetValue(name, out var value)
            ? value
            : throw new ScriptFormatException($"{Join(path, name)}がありません。");
    }

    /// <summary>
    /// 必須項目が配列であることを確認して取得する。
    /// </summary>
    /// <param name="properties">取得元の項目辞書</param>
    /// <param name="path">objectのJSON上の位置</param>
    /// <param name="name">必須の配列の項目名</param>
    /// <returns>指定した項目の配列要素 配列の各要素の種類は検査しない</returns>
    /// <exception cref="ScriptFormatException">指定した項目がないか、配列ではない場合</exception>
    private static JsonElement GetArray(
        Dictionary<string, JsonElement> properties,
        string path,
        string name
    )
    {
        var value = GetRequired(properties, path, name);
        return value.ValueKind == JsonValueKind.Array
            ? value
            : throw new ScriptFormatException($"{Join(path, name)}が配列ではありません。");
    }

    /// <summary>
    /// 必須項目を取得可能な文字列として読み、内容を加工せず返す。
    /// </summary>
    /// <param name="properties">取得元の項目辞書</param>
    /// <param name="path">objectのJSON上の位置</param>
    /// <param name="name">必須の文字列の項目名</param>
    /// <returns>指定した項目の文字列 空文字列もそのまま返す</returns>
    /// <exception cref="ScriptFormatException">指定した項目がないか、取得可能な文字列ではない場合</exception>
    private static string GetString(
        Dictionary<string, JsonElement> properties,
        string path,
        string name
    )
    {
        return ToString(GetRequired(properties, path, name), Join(path, name));
    }

    /// <summary>
    /// 任意の文字列項目を読み、存在する項目が不正なら省略として扱わず拒否する。
    /// </summary>
    /// <param name="properties">取得元の項目辞書</param>
    /// <param name="path">objectのJSON上の位置</param>
    /// <param name="name">任意の文字列の項目名</param>
    /// <returns>加工しない文字列 項目が存在しない場合だけnull</returns>
    /// <exception cref="ScriptFormatException">指定した項目はあるが取得可能な文字列ではない場合</exception>
    private static string? GetOptionalString(
        Dictionary<string, JsonElement> properties,
        string path,
        string name
    )
    {
        return properties.TryGetValue(name, out var value)
            ? ToString(value, Join(path, name))
            : null;
    }

    /// <summary>
    /// JSON値を文字列として取得し、不正な文字列も位置付きの読み取り異常にする。
    /// </summary>
    /// <param name="value">文字列として読み取るJSON値</param>
    /// <param name="path">値の異常を報告するJSON上の位置</param>
    /// <returns>JSONのエスケープを解釈して取得した文字列 内容の正規化は行わない</returns>
    /// <exception cref="ScriptFormatException">文字列ではないか、不正なUTF-8やサロゲートを含んで取得できない場合</exception>
    private static string ToString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ScriptFormatException($"{path}が文字列ではありません。");
        }

        try
        {
            return value.GetString()!;
        }
        catch (InvalidOperationException)
        {
            // 不正なUTF-8やサロゲートを含む文字列は取得できない値とする。
            throw new ScriptFormatException($"{path}を文字列として取得できません。");
        }
    }

    /// <summary>
    /// 不正なcommandから更新対象の名前を取得できる場合だけ取り出す。
    /// </summary>
    /// <param name="element">不正なcommandのJSON要素</param>
    /// <param name="name">失敗状態を残す対象の項目名</param>
    /// <returns>同名項目が1個だけで取得可能な文字列の場合はその内容 それ以外はnull</returns>
    private static string? GetSingleString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // 同名の項目が複数ある場合や文字列として取得できない場合は、名前を推定しない。
        var values = element.EnumerateObject().Where(x => NameEquals(x, name)).ToArray();
        if (values is not [{ Value.ValueKind: JsonValueKind.String } property])
        {
            return null;
        }

        try
        {
            return property.Value.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// JSONの項目名との一致を調べ、取得できない項目名は不一致として扱う。
    /// </summary>
    /// <param name="property">比較するJSONの項目</param>
    /// <param name="name">比較対象の項目名</param>
    /// <returns>大文字小文字まで一致する場合はtrue 不正な項目名の場合はfalse</returns>
    private static bool NameEquals(JsonProperty property, string name)
    {
        try
        {
            return property.NameEquals(name);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 診断に使うJSON上の位置へ項目名を追加する。
    /// </summary>
    /// <param name="path">親の位置 ルートでは空文字列</param>
    /// <param name="name">追加する項目名</param>
    /// <returns>親の位置があればピリオドで結んだ位置、なければ項目名だけの位置</returns>
    private static string Join(string path, string name)
    {
        return path.Length == 0 ? name : $"{path}.{name}";
    }

    /// <summary>
    /// 固定形式外の種類名や型名を、位置と元の値を持つ読み取り異常にする。
    /// </summary>
    /// <param name="path">未知の値があるJSON上の位置</param>
    /// <param name="value">読み取った元の値の文字列</param>
    /// <returns>位置と加工しない元の値をメッセージに含む例外</returns>
    private static ScriptFormatException Unknown(string path, string value)
    {
        return new($"{path}の{value}は固定形式にない値です。");
    }
}
