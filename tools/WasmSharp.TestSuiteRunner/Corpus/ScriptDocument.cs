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
    /// <summary>
    /// JSON全体の構造不正やcommand列挙の失敗を診断で識別する操作名
    /// </summary>
    private const string OPERATION = "enumerate";

    /// <summary>
    /// 元入力の相対pathを取得するrootの項目名
    /// </summary>
    private const string SOURCE_FILENAME = "source_filename";

    /// <summary>
    /// 公式ケースを列挙するrootの配列の項目名
    /// </summary>
    private const string COMMANDS = "commands";

    /// <summary>
    /// 所有する元バイト列
    /// </summary>
    internal ImmutableArray<byte> Content { get; }

    /// <summary>
    /// JSONに記録されたsource_filename 取得できない場合はnull
    /// </summary>
    internal string? SourceFilename { get; }

    /// <summary>
    /// 境界を確定できたcommandの順序付き一覧 構文破損時は破損前に確定したcommandだけを含む
    /// </summary>
    internal ImmutableArray<EnumeratedCommand> Commands { get; }

    /// <summary>
    /// JSON全体の構文を確認し、唯一のcommands配列を最後まで列挙したかどうか
    /// </summary>
    internal bool EnumerationComplete { get; }

    /// <summary>
    /// 確定したcommand総数 列挙を完了できない場合はnull
    /// </summary>
    internal int? CommandCount => EnumerationComplete ? Commands.Length : null;

    /// <summary>
    /// 構文破損や全体の必須項目の不正を示す診断 command単位の構造不正は含めない
    /// </summary>
    internal ImmutableArray<CorpusDiagnostic> Diagnostics { get; }

    /// <summary>
    /// コピー済みの元バイト列と、そこから確定できたcommand・診断を保持する。
    /// </summary>
    /// <param name="content">このdocumentが所有する生成JSONの生バイト列</param>
    /// <param name="sourceFilename">取得できた元入力の相対path 取得できない場合はnull</param>
    /// <param name="commands">元バイト列の範囲が確定したcommandの順序付き一覧</param>
    /// <param name="enumerationComplete">唯一のcommands配列の列挙とJSON全体の構文確認を完了したかどうか</param>
    /// <param name="diagnostics">JSON全体の構文破損や必須項目の不正を示す診断</param>
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
    /// <returns>元バイト列のコピーと列挙結果 構文破損時も破損前に確定したcommandと失敗理由を保持し、総数を未確定にする</returns>
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
    /// <returns>元バイト列を共有するcommandの読み取り専用メモリ 新たなコピーは作らない</returns>
    /// <exception cref="IndexOutOfRangeException">indexが列挙済みcommandの範囲外の場合</exception>
    internal ReadOnlyMemory<byte> GetJson(int index)
    {
        var command = Commands[index];
        return Content.AsMemory().Slice(command.Start, command.Length);
    }

    /// <summary>
    /// rootの必須項目とcommand配列を読み取り、全体構造の不正を診断へ追加する。
    /// </summary>
    /// <param name="reader">JSONの先頭から読み進めるreader 正常なrootでは終端まで進める</param>
    /// <param name="content">readerが参照する、このdocumentが所有する元バイト列</param>
    /// <param name="commands">境界を確定できたcommandを追加する一覧</param>
    /// <param name="diagnostics">必須項目の欠落・重複・型不正や未知の項目を追加する診断一覧</param>
    /// <param name="path">診断に記録するJSONの相対path</param>
    /// <param name="sourceFilename">取得した元入力の相対pathを設定する変数 重複・型不正の場合はnull</param>
    /// <returns>唯一のcommands配列を最後まで列挙できた場合はtrue 他の必須項目の診断がある場合も列挙完了とは区別する</returns>
    /// <exception cref="JsonException">JSONの構文が破損している場合</exception>
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

    /// <summary>
    /// commands配列の各要素を元の順序で列挙し、元バイト列の範囲と取得できた項目を追加する。
    /// </summary>
    /// <param name="reader">commands配列の開始位置にあるreader 正常終了時は配列の終端まで進める</param>
    /// <param name="content">readerが参照する、このdocumentが所有する元バイト列</param>
    /// <param name="commands">確定したcommandを追加する一覧 構文破損時も追加済みの要素を保持する</param>
    /// <exception cref="JsonException">配列の途中でJSONの構文破損を検出した場合</exception>
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

    /// <summary>
    /// 境界を確定したJSONから、commandの位置情報と一意に取得できる項目を取り出す。
    /// </summary>
    /// <param name="index">入力内の0始まりcommand index</param>
    /// <param name="start">所有する元バイト列におけるcommandの開始位置</param>
    /// <param name="json">構文確認済みのcommand要素を表すJSON</param>
    /// <returns>JSONの範囲と取得できた項目 objectでない要素、項目の欠落・重複・型不正は該当する値をnullにする</returns>
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

    /// <summary>
    /// 指定した名前の項目が一つだけ存在する場合に、その値を取得する。
    /// </summary>
    /// <param name="properties">commandの全項目</param>
    /// <param name="name">取得する項目の名前</param>
    /// <returns>一意に特定できた項目の値 欠落または重複の場合はnull</returns>
    private static JsonElement? GetSingle(JsonProperty[] properties, string name)
    {
        // 同名の項目が複数ある場合は、どちらの値も採用せず取得できない値とする。
        return properties.Where(x => NameEquals(x, name)).ToArray() is [var property]
            ? property.Value
            : null;
    }

    /// <summary>
    /// readerの項目名を指定した名前と比較し、不正なエスケープを含む名前は一致しないものとして扱う。
    /// </summary>
    /// <param name="reader">比較する項目名の位置にあるreader 位置は変更しない</param>
    /// <param name="name">固定形式の項目名</param>
    /// <returns>エスケープを解釈した項目名が一致する場合はtrue 文字列を解釈できない場合はfalse</returns>
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

    /// <summary>
    /// JSONの項目名を指定した名前と比較し、不正なエスケープを含む名前は一致しないものとして扱う。
    /// </summary>
    /// <param name="property">比較するJSONの項目</param>
    /// <param name="name">固定形式の項目名</param>
    /// <returns>エスケープを解釈した項目名が一致する場合はtrue 文字列を解釈できない場合はfalse</returns>
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

    /// <summary>
    /// JSON要素を文字列として取得し、型不正や文字列を解釈できない場合は値を採用しない。
    /// </summary>
    /// <param name="element">取得するJSON要素 項目がないか重複している場合はnull</param>
    /// <returns>取得した文字列 要素がない、文字列型でない、またはUTF-8やサロゲートが不正な場合はnull</returns>
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

    /// <summary>
    /// readerの現在値を文字列として取得し、型不正や文字列を解釈できない場合は値を採用しない。
    /// </summary>
    /// <param name="reader">取得する値の位置にあるreader 位置は変更しない</param>
    /// <returns>取得した文字列 文字列型でない、またはUTF-8やサロゲートが不正な場合はnull</returns>
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
