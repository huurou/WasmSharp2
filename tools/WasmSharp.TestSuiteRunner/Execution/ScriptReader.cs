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
    private const string OPERATION = "read_command";
    private const string TYPE = "type";
    private const string LINE = "line";
    private const string NAME = "name";
    private const string FILENAME = "filename";
    private const string MODULE_TYPE = "module_type";
    private const string AS = "as";
    private const string ACTION = "action";
    private const string TEXT = "text";
    private const string EXPECTED = "expected";
    private const string MODULE = "module";
    private const string FIELD = "field";
    private const string ARGS = "args";
    private const string VALUE = "value";
    private const string LANE_TYPE = "lane_type";

    /// <summary>
    /// 列挙済みの全commandを順序どおりに読み取る。読み取れないcommandも同じ位置に1件として残す。
    /// </summary>
    /// <param name="document">照合済みのJSON</param>
    /// <param name="inputPath">test/core基準の入力相対path</param>
    internal static ScriptReadResult Read(ScriptDocument document, string inputPath)
    {
        return new(
            inputPath,
            [.. document.Commands.Select(x => ReadCommand(document.GetJson(x.Index), x))],
            document.EnumerationComplete
        );
    }

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

    private static ImmutableArray<T> ReadValues<T>(
        JsonElement array,
        string path,
        Func<WasmValueKind, string?, LaneType?, ImmutableArray<string>, T> create
    )
    {
        return [.. array.EnumerateArray().Select((x, i) => ReadValue(x, $"{path}[{i}]", create))];
    }

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

    private static int ReadLine(Dictionary<string, JsonElement> properties)
    {
        return
            GetRequired(properties, "", LINE) is { ValueKind: JsonValueKind.Number } line
            && line.TryGetInt32(out var value)
            && value > 0
            ? value
            : throw new ScriptFormatException($"{LINE}が1以上の整数ではありません。");
    }

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

    private static ScriptModuleType ParseModuleType(string moduleType)
    {
        return moduleType switch
        {
            "binary" => ScriptModuleType.Binary,
            "text" => ScriptModuleType.Text,
            _ => throw Unknown(MODULE_TYPE, moduleType),
        };
    }

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

    private static string GetString(
        Dictionary<string, JsonElement> properties,
        string path,
        string name
    )
    {
        return ToString(GetRequired(properties, path, name), Join(path, name));
    }

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

    private static string Join(string path, string name)
    {
        return path.Length == 0 ? name : $"{path}.{name}";
    }

    private static ScriptFormatException Unknown(string path, string value)
    {
        return new($"{path}の{value}は固定形式にない値です。");
    }
}
