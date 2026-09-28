using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 保存済み結果のschemaと記録の完全性を検証し、JSONの確定保存と読取を行う
/// </summary>
internal static class ReportStore
{
    private const int SCHEMA_VERSION = 1;
    private const string MANIFEST_KIND = "corpus_manifest";
    private const string RUN_REPORT_KIND = "run_report";

    /// <summary>
    /// 重複キーを構文破損と同様に拒否するJSON読取設定
    /// </summary>
    private static readonly JsonDocumentOptions documentOptions_ = new()
    {
        AllowDuplicateProperties = false,
    };

    /// <summary>
    /// 未知のpropertyや必須項目の欠落、数値や複合表記による未定義の分類を無言で読み替えないDTO読取設定
    /// </summary>
    private static readonly JsonSerializerOptions readOptions_ = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new StrictEnumConverter() },
    };

    /// <summary>
    /// OSに依存しない改行で、日本語の診断を読める形のまま書き出す設定
    /// </summary>
    private static readonly JsonSerializerOptions writeOptions_ = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// manifestを確定保存する。既存ファイルは上書きしない。
    /// </summary>
    /// <exception cref="ReportStoreException">保存を確定できない場合</exception>
    internal static void Save(CorpusManifest manifest, string path)
    {
        Write(
            path,
            replaceExisting: false,
            x => JsonSerializer.Serialize(x, manifest, writeOptions_)
        );
    }

    /// <summary>
    /// 実行結果を確定保存する。既存ファイルは上書きしない。
    /// </summary>
    /// <exception cref="ReportStoreException">保存を確定できない場合</exception>
    internal static void Save(RunReport report, string path)
    {
        Write(
            path,
            replaceExisting: false,
            x => JsonSerializer.Serialize(x, report, writeOptions_)
        );
    }

    /// <summary>
    /// 比較結果を確定保存する。既存ファイルは上書きしない。
    /// </summary>
    /// <exception cref="ReportStoreException">保存を確定できない場合</exception>
    internal static void Save(ComparisonReport report, string path)
    {
        Write(
            path,
            replaceExisting: false,
            x => JsonSerializer.Serialize(x, report, writeOptions_)
        );
    }

    /// <summary>
    /// 検証済みの保存JSONを同じ内容のbaselineとして確定保存し、既存baselineを明示的に置換する。
    /// </summary>
    /// <exception cref="ReportStoreException">保存を確定できない場合。既存baselineは変更しない</exception>
    internal static void SaveBaseline(byte[] content, string path)
    {
        Write(path, replaceExisting: true, x => x.Write(content));
    }

    /// <summary>
    /// 保存先と同じディレクトリの一時ファイルへ書き切ってから確定する。
    /// 置換しない場合は、確定の直前に作られたファイルも上書きしない。
    /// </summary>
    /// <remarks>
    /// 書込みの失敗や中断では一時ファイルを削除し、既存の保存先を変更しない。
    /// </remarks>
    /// <exception cref="ReportStoreException">書込みまたは確定のファイル操作に失敗した場合</exception>
    internal static void Write(string path, bool replaceExisting, Action<Stream> write)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp"
        );
        try
        {
            using (
                var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, replaceExisting);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportStoreException(
                $"{fullPath}へ結果を保存できません: {ex.Message}",
                fullPath,
                ex
            );
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// 保存済みのmanifestまたは実行結果を読み取り、記録の問題とともに返す。
    /// </summary>
    /// <exception cref="ReportStoreException">ファイル・schema・kind・JSON構造・ケース識別を読み取れない場合</exception>
    internal static StoredReport Read(string path)
    {
        var fullPath = Path.GetFullPath(path);
        byte[] content;
        try
        {
            content = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportStoreException(
                $"{fullPath}を読み取れません: {ex.Message}",
                fullPath,
                ex
            );
        }

        try
        {
            using var document = JsonDocument.Parse(content, documentOptions_);
            var root = document.RootElement;
            if (ReadKind(root, fullPath) == MANIFEST_KIND)
            {
                var manifest = Deserialize<CorpusManifest>(root);
                return new StoredManifest(manifest, content, Validate(manifest));
            }

            var report = Deserialize<RunReport>(root);
            EnsureCaseIds(report, fullPath);
            return new StoredRunReport(report, content, Validate(report));
        }
        catch (JsonException ex)
        {
            throw new ReportStoreException(
                $"{fullPath}を保存済み結果のJSONとして読み取れません: {ex.Message}",
                fullPath,
                ex
            );
        }
    }

    /// <summary>
    /// 保存済みのmanifestだけを読み取る。
    /// </summary>
    /// <exception cref="ReportStoreException">読み取れない場合、またはmanifest以外の場合</exception>
    internal static StoredManifest ReadManifest(string path)
    {
        return Read(path) as StoredManifest
            ?? throw new ReportStoreException(
                $"{Path.GetFullPath(path)}は{MANIFEST_KIND}ではありません。",
                Path.GetFullPath(path)
            );
    }

    /// <summary>
    /// 保存済みの実行結果だけを読み取る。
    /// </summary>
    /// <exception cref="ReportStoreException">読み取れない場合、または実行結果以外の場合</exception>
    internal static StoredRunReport ReadRunReport(string path)
    {
        return Read(path) as StoredRunReport
            ?? throw new ReportStoreException(
                $"{Path.GetFullPath(path)}は{RUN_REPORT_KIND}ではありません。",
                Path.GetFullPath(path)
            );
    }

    /// <summary>
    /// 保存された完了flagだけを信用せず、profileの全入力・未処理・集計に照らしてmanifestを検証する。
    /// </summary>
    internal static ImmutableArray<RecordIssue> Validate(CorpusManifest manifest)
    {
        var issues = ImmutableArray.CreateBuilder<RecordIssue>();
        AddInputSetIssues(
            manifest.Profile.Inputs,
            manifest.Inputs.Select(x => x.Input.Path),
            issues
        );
        foreach (var input in manifest.Inputs.Where(x => x.Status == ConversionStatus.Unprocessed))
        {
            issues.Add(new(RecordIssueKind.Unprocessed, $"{input.Input.Path}は未処理です。"));
        }

        AddCompletionIssues(
            manifest.Completion.ProcessingComplete,
            manifest.Completion.OutputComplete,
            manifest.Inputs.Any(x => x.Status == ConversionStatus.Unprocessed),
            issues
        );
        if (manifest.Summarize() != manifest.Summary)
        {
            issues.Add(new(RecordIssueKind.Inconsistent, "集計が記録内容と一致しません。"));
        }

        return issues.ToImmutable();
    }

    /// <summary>
    /// 保存された完了flagだけを信用せず、profileの全入力と素材のcommand一覧に照らして実行結果を検証する。
    /// </summary>
    internal static ImmutableArray<RecordIssue> Validate(RunReport report)
    {
        var issues = ImmutableArray.CreateBuilder<RecordIssue>();
        // 重複した素材からcommand件数の基準を黙って選ばないよう、素材スナップショットの入力集合と集計も確かめる。
        AddInputSetIssues(
            report.Corpus.Profile.Inputs,
            report.Corpus.Inputs.Select(x => x.Input.Path),
            issues,
            "素材の"
        );
        if (report.Corpus.Summarize() != report.Corpus.Summary)
        {
            issues.Add(new(RecordIssueKind.Inconsistent, "素材の集計が記録内容と一致しません。"));
        }

        AddInputSetIssues(
            report.Corpus.Profile.Inputs,
            report.Inputs.Select(x => x.InputPath),
            issues
        );
        var materialCommandCounts = report
            .Corpus.Inputs.GroupBy(x => x.Input.Path, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => GetCommandCount(x.First()), StringComparer.Ordinal);
        foreach (var input in report.Inputs)
        {
            AddInputIssues(input, materialCommandCounts.GetValueOrDefault(input.InputPath), issues);
        }

        AddCompletionIssues(
            report.Completion.ProcessingComplete,
            report.Completion.OutputComplete,
            report.Inputs.Any(x => x.Status != InputRunStatus.Processed),
            issues
        );
        if (report.Summarize() != report.Summary)
        {
            issues.Add(new(RecordIssueKind.Inconsistent, "集計が記録内容と一致しません。"));
        }

        return issues.ToImmutable();
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 保存失敗・中断の元の理由を優先し、一時ファイルの削除失敗で置き換えない。
        }
    }

    private static string ReadKind(JsonElement root, string path)
    {
        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schema_version", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var number)
            || number != SCHEMA_VERSION
        )
        {
            throw new ReportStoreException(
                $"{path}は対応するschema_version={SCHEMA_VERSION}の保存済み結果ではありません。",
                path
            );
        }

        if (
            !root.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String
            || kind.GetString() is not (MANIFEST_KIND or RUN_REPORT_KIND)
        )
        {
            throw new ReportStoreException(
                $"{path}のkindは{MANIFEST_KIND}または{RUN_REPORT_KIND}ではありません。",
                path
            );
        }

        return kind.GetString()!;
    }

    private static T Deserialize<T>(JsonElement root)
    {
        EnsureNoNullElements(root);
        return root.Deserialize<T>(readOptions_) ?? throw new JsonException("結果がnullです。");
    }

    private static void EnsureNoNullElements(JsonElement element)
    {
        // null許容注釈では一覧の要素を検査できない。保存形式の一覧は要素にnullを持たないため、構造不正として拒否する。
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                EnsureNoNullElements(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    throw new JsonException("一覧にnullの要素があります。");
                }

                EnsureNoNullElements(item);
            }
        }
    }

    private static void EnsureCaseIds(RunReport report, string path)
    {
        foreach (var input in report.Inputs)
        {
            foreach (
                var item in input.Cases.Where(x =>
                    x.Id.CommandIndex < 0 || x.Id.InputPath != input.InputPath
                )
            )
            {
                throw new ReportStoreException(
                    $"{path}のケース識別{item.Id}を{input.InputPath}の記録として読み取れません。",
                    path
                );
            }
        }
    }

    private static int? GetCommandCount(InputConversionResult input)
    {
        // 件数だけを信用せず、列挙を完了して保存されたcommand一覧と件数が一致する素材だけを基準にする。
        return
            input.Artifacts.Where(x => x.Kind == ArtifactKind.Json).ToArray()
                is [{ Script: { EnumerationComplete: true, CommandCount: { } count } script }]
            && count == script.Commands.Count
            ? count
            : null;
    }

    private static void AddInputSetIssues(
        IEnumerable<SourceInput> expected,
        IEnumerable<string> actual,
        ImmutableArray<RecordIssue>.Builder issues,
        string prefix = ""
    )
    {
        var expectedPaths = expected.Select(x => x.Path).ToArray();
        var counts = actual
            .CountBy(x => x, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        foreach (var path in expectedPaths.Where(x => !counts.ContainsKey(x)))
        {
            issues.Add(new(RecordIssueKind.Missing, $"{prefix}{path}の記録がありません。"));
        }

        foreach (var (path, count) in counts)
        {
            if (count > 1)
            {
                issues.Add(
                    new(RecordIssueKind.Duplicate, $"{prefix}{path}の記録が{count}件あります。")
                );
            }

            if (!expectedPaths.Contains(path, StringComparer.Ordinal))
            {
                issues.Add(
                    new(
                        RecordIssueKind.Unexpected,
                        $"{prefix}{path}はprofileの対象入力ではありません。"
                    )
                );
            }
        }
    }

    private static void AddInputIssues(
        InputRunResult input,
        int? materialCommandCount,
        ImmutableArray<RecordIssue>.Builder issues
    )
    {
        var path = input.InputPath;
        switch (input.Status)
        {
            case InputRunStatus.Unprocessed:
                issues.Add(new(RecordIssueKind.Unprocessed, $"{path}は未処理です。"));
                break;
            case InputRunStatus.Incomplete:
                issues.Add(
                    new(
                        RecordIssueKind.Unprocessed,
                        $"{path}の処理が完了していません。未処理commandは{input.UnprocessedCount}件です。"
                    )
                );
                break;
            case InputRunStatus.Processed when input.UnprocessedCount != 0:
                issues.Add(
                    new(
                        RecordIssueKind.Inconsistent,
                        $"処理済みの{path}に未処理commandが{input.UnprocessedCount}件記録されています。"
                    )
                );
                break;
        }

        if (input.Status != InputRunStatus.Unprocessed && input.CommandCount is null)
        {
            issues.Add(new(RecordIssueKind.Undetermined, $"{path}のcommand件数が未確定です。"));
        }

        if (
            input.CommandCount is { } count
            && (count != input.EnumeratedCount || count != materialCommandCount)
        )
        {
            issues.Add(
                new(
                    RecordIssueKind.Inconsistent,
                    $"{path}のcommand件数{count}が列挙済み数{input.EnumeratedCount}または素材のcommand件数{materialCommandCount?.ToString() ?? "未確定"}と一致しません。"
                )
            );
        }

        if (input.UnprocessedCount < 0 || input.UnprocessedCount > input.EnumeratedCount)
        {
            issues.Add(
                new(
                    RecordIssueKind.Inconsistent,
                    $"{path}の未処理command数{input.UnprocessedCount}が列挙済み数{input.EnumeratedCount}と整合しません。"
                )
            );
            return;
        }

        // 順に処理するため、未処理commandは列挙済みの末尾に位置する。
        var processedCount = input.EnumeratedCount - input.UnprocessedCount;
        foreach (var group in input.Cases.GroupBy(x => x.Id.CommandIndex).OrderBy(x => x.Key))
        {
            var id = new CaseId(path, group.Key);
            if (group.Count() > 1)
            {
                issues.Add(
                    new(RecordIssueKind.Duplicate, $"{id}の結果が{group.Count()}件あります。")
                );
            }

            if (group.Key >= processedCount)
            {
                issues.Add(new(RecordIssueKind.Unexpected, $"{id}は処理済みcommandの範囲外です。"));
            }
        }

        var recorded = input.Cases.Select(x => x.Id.CommandIndex).ToHashSet();
        foreach (var index in Enumerable.Range(0, processedCount).Where(x => !recorded.Contains(x)))
        {
            issues.Add(
                new(RecordIssueKind.Missing, $"{new CaseId(path, index)}の結果がありません。")
            );
        }

        foreach (
            var item in input.Cases.Where(x =>
                x.Category is null && x.Outcome != CaseOutcome.RunnerError
            )
        )
        {
            issues.Add(
                new(
                    RecordIssueKind.Inconsistent,
                    $"種類未確定の{item.Id}がrunner_error以外に分類されています。"
                )
            );
        }
    }

    private static void AddCompletionIssues(
        bool processingComplete,
        bool outputComplete,
        bool hasUnprocessed,
        ImmutableArray<RecordIssue>.Builder issues
    )
    {
        if (!processingComplete)
        {
            issues.Add(new(RecordIssueKind.Unprocessed, "処理の完了が記録されていません。"));
        }
        else if (hasUnprocessed)
        {
            issues.Add(
                new(
                    RecordIssueKind.Inconsistent,
                    "処理の完了が記録されていますが、未処理の対象があります。"
                )
            );
        }

        if (!outputComplete)
        {
            issues.Add(new(RecordIssueKind.OutputFailed, "出力の完了が記録されていません。"));
        }
    }
}
