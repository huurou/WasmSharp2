using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成JSONの元バイト列を所有し、command境界と素材参照を列挙した結果
/// </summary>
/// <remarks>
/// 生成時の参照収集と実行時の型変換で同じ内容を共有する。commandの実行意味・引数・期待値は解釈しない。
/// </remarks>
internal sealed class ScriptDocument
{
    private const string OPERATION = "enumerate";
    private const string SOURCE_FILENAME = "source_filename";
    private const string COMMANDS = "commands";

    /// <summary>
    /// 所有する元バイト列
    /// </summary>
    internal ImmutableArray<byte> Content { get; }

    /// <summary>
    /// JSONに記録されたsource_filename。取得できない場合はnull
    /// </summary>
    internal string? SourceFilename { get; }

    /// <summary>
    /// 境界を確定できたcommandの順序付き一覧。構文破損時は破損前に確定したcommandだけを含む
    /// </summary>
    internal ImmutableArray<EnumeratedCommand> Commands { get; }

    /// <summary>
    /// JSON全体の構文を確認し、唯一のcommands配列を最後まで列挙したかどうか
    /// </summary>
    internal bool EnumerationComplete { get; }

    /// <summary>
    /// 確定したcommand総数。列挙を完了できない場合はnull
    /// </summary>
    internal int? CommandCount => EnumerationComplete ? Commands.Length : null;

    /// <summary>
    /// 構文破損や全体の必須項目の不正を示す診断。command単位の構造不正は含めない
    /// </summary>
    internal ImmutableArray<CorpusDiagnostic> Diagnostics { get; }

    private ScriptDocument(
        ImmutableArray<byte> content,
        string? sourceFilename,
        ImmutableArray<EnumeratedCommand> commands,
        bool enumerationComplete,
        ImmutableArray<CorpusDiagnostic> diagnostics
    )
    {
        Content = content;
        SourceFilename = sourceFilename;
        Commands = commands;
        EnumerationComplete = enumerationComplete;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 元バイト列をコピーして所有し、全体の必須項目とcommand境界を読み取る。
    /// </summary>
    /// <param name="content">生成JSONの生バイト列</param>
    /// <param name="path">診断に記録するmanifest基準の相対path</param>
    internal static ScriptDocument Parse(ReadOnlySpan<byte> content, string path)
    {
        var owned = content.ToImmutableArray();
        var commands = ImmutableArray.CreateBuilder<EnumeratedCommand>();
        var diagnostics = ImmutableArray.CreateBuilder<CorpusDiagnostic>();
        string? sourceFilename = null;
        var complete = false;
        var reader = new Utf8JsonReader(owned.AsSpan());
        try
        {
            complete = ReadRoot(ref reader, owned, commands, diagnostics, path, ref sourceFilename);
        }
        catch (JsonException ex)
        {
            // 破損位置より後は境界を確定できないため、確定済みのcommandだけを残して総数を未確定にする。
            complete = false;
            var position = ex.LineNumber is { } line
                ? $"{line + 1}行目の{ex.BytePositionInLine + 1}バイト目"
                : "位置不明の箇所";
            diagnostics.Add(
                new(
                    OPERATION,
                    $"JSONの構文が{position}で破損しているため、確定済みの{commands.Count}件より後のcommandを列挙できません: {ex.Message}",
                    path,
                    ex.GetType().FullName
                )
            );
        }

        return new(
            owned,
            sourceFilename,
            commands.ToImmutable(),
            complete,
            diagnostics.ToImmutable()
        );
    }

    /// <summary>
    /// 列挙済みcommandの元JSONを、所有するバイト列の範囲として返す。
    /// </summary>
    /// <param name="index">0始まりのcommand index</param>
    internal ReadOnlyMemory<byte> GetJson(int index)
    {
        var command = Commands[index];
        return Content.AsMemory().Slice(command.Start, command.Length);
    }

    private static bool ReadRoot(
        ref Utf8JsonReader reader,
        ImmutableArray<byte> content,
        ImmutableArray<EnumeratedCommand>.Builder commands,
        ImmutableArray<CorpusDiagnostic>.Builder diagnostics,
        string path,
        ref string? sourceFilename
    )
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            diagnostics.Add(new(OPERATION, "JSONのrootがobjectではありません。", path));
            return false;
        }

        var hasSourceFilename = false;
        var commandsCount = 0;
        var enumerated = false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (NameEquals(ref reader, SOURCE_FILENAME))
            {
                reader.Read();
                if (hasSourceFilename)
                {
                    // どちらの値が元入力を示すか決められないため、取得できない値として扱う。
                    sourceFilename = null;
                    diagnostics.Add(new(OPERATION, $"{SOURCE_FILENAME}が重複しています。", path));
                }
                else if ((sourceFilename = GetString(ref reader)) is null)
                {
                    diagnostics.Add(
                        new(OPERATION, $"{SOURCE_FILENAME}を文字列として取得できません。", path)
                    );
                }

                hasSourceFilename = true;
                reader.Skip();
            }
            else if (NameEquals(ref reader, COMMANDS))
            {
                reader.Read();
                commandsCount++;
                if (commandsCount > 1)
                {
                    // どちらが本来の一覧か決められないため、総数を未確定にする。
                    enumerated = false;
                    diagnostics.Add(new(OPERATION, $"{COMMANDS}が重複しています。", path));
                    reader.Skip();
                }
                else if (reader.TokenType != JsonTokenType.StartArray)
                {
                    diagnostics.Add(new(OPERATION, $"{COMMANDS}が配列ではありません。", path));
                    reader.Skip();
                }
                else
                {
                    ReadCommands(ref reader, content, commands);
                    enumerated = true;
                }
            }
            else
            {
                var name = Encoding.UTF8.GetString(reader.ValueSpan);
                reader.Read();
                diagnostics.Add(new(OPERATION, $"固定形式にない{name}があります。", path));
                reader.Skip();
            }
        }

        if (!hasSourceFilename)
        {
            diagnostics.Add(new(OPERATION, $"{SOURCE_FILENAME}がありません。", path));
        }

        if (commandsCount == 0)
        {
            diagnostics.Add(new(OPERATION, $"{COMMANDS}がありません。", path));
        }

        // root以降の余分な内容も構文破損として検出する。欠落の診断は先に記録しておく。
        reader.Read();

        return enumerated;
    }

    private static void ReadCommands(
        ref Utf8JsonReader reader,
        ImmutableArray<byte> content,
        ImmutableArray<EnumeratedCommand>.Builder commands
    )
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var start = (int)reader.TokenStartIndex;
            reader.Skip();
            var length = (int)reader.BytesConsumed - start;
            commands.Add(
                CreateCommand(commands.Count, start, content.AsMemory().Slice(start, length))
            );
        }
    }

    private static EnumeratedCommand CreateCommand(int index, int start, ReadOnlyMemory<byte> json)
    {
        // 境界は確定済みのため再読取は失敗しない。構造不正は取得できない項目としてnullにとどめる。
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new(index, start, json.Length, null, null, null, null);
        }

        var properties = document.RootElement.EnumerateObject().ToArray();
        return new(
            index,
            start,
            json.Length,
            GetSingle(properties, "line") is { ValueKind: JsonValueKind.Number } line
            && line.TryGetInt32(out var value)
            && value > 0
                ? value
                : null,
            GetString(GetSingle(properties, "type")),
            GetString(GetSingle(properties, "module_type")),
            GetString(GetSingle(properties, "filename"))
        );
    }

    private static JsonElement? GetSingle(JsonProperty[] properties, string name)
    {
        // 同名の項目が複数ある場合は、どちらの値も採用せず取得できない値とする。
        return properties.Where(x => NameEquals(x, name)).ToArray() is [var property]
            ? property.Value
            : null;
    }

    private static bool NameEquals(ref Utf8JsonReader reader, string name)
    {
        try
        {
            return reader.ValueTextEquals(name);
        }
        catch (InvalidOperationException)
        {
            // 対になるサロゲートを欠くエスケープを含む名前は、固定形式のどの項目名とも一致しない。
            return false;
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
            // 対になるサロゲートを欠くエスケープを含む名前は、固定形式のどの項目名とも一致しない。
            return false;
        }
    }

    private static string? GetString(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.String } value)
        {
            return null;
        }

        try
        {
            return value.GetString();
        }
        catch (InvalidOperationException)
        {
            // 不正なUTF-8やサロゲートを含む文字列は取得できない値とする。
            return null;
        }
    }

    private static string? GetString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return null;
        }

        try
        {
            return reader.GetString();
        }
        catch (InvalidOperationException)
        {
            // 不正なUTF-8やサロゲートを含む文字列は取得できない値とする。
            return null;
        }
    }
}
