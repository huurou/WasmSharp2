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
    /// <summary>
    /// 読取りを受け付ける保存形式の版
    /// </summary>
    private const int SCHEMA_VERSION = 1;

    /// <summary>
    /// 変換manifestを識別する保存形式の種類
    /// </summary>
    private const string MANIFEST_KIND = "corpus_manifest";

    /// <summary>
    /// 実行結果を識別する保存形式の種類
    /// </summary>
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
    /// <param name="manifest">保存する変換結果 保存前の完全性検証は呼出側で行う</param>
    /// <param name="path">新規作成するJSONファイルの保存先 親ディレクトリは作成済みであること</param>
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
    /// <param name="report">保存する実行結果 保存前の完全性検証は呼出側で行う</param>
    /// <param name="path">新規作成するJSONファイルの保存先 親ディレクトリは作成済みであること</param>
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
    /// <param name="report">保存する比較結果</param>
    /// <param name="path">新規作成するJSONファイルの保存先 親ディレクトリは作成済みであること</param>
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
    /// <param name="content">呼出側で検証済みのJSONバイト列 内容の再検証や整形は行わない</param>
    /// <param name="path">baselineの保存先 親ディレクトリは作成済みであること</param>
    /// <exception cref="ReportStoreException">保存を確定できない場合 既存baselineは変更しない</exception>
    internal static void SaveBaseline(byte[] content, string path)
    {
        Write(path, replaceExisting: true, x => x.Write(content));
    }

    /// <summary>
    /// 保存先と同じディレクトリの一時ファイルへ書き切ってから確定する。
    /// 置換しない場合は、確定の直前に作られたファイルも上書きしない。
    /// </summary>
    /// <param name="path">保存先ファイル 親ディレクトリは作成済みであること</param>
    /// <param name="replaceExisting">既存の保存先ファイルを置換する場合はtrue</param>
    /// <param name="write">一時ファイルへ内容を書き込む処理 渡されたStreamの破棄はこのメソッドが行う</param>
    /// <remarks>
    /// 書込みの失敗や中断では既存の保存先を変更せず、一時ファイルの削除を試みる。
    /// 削除に失敗しても元の例外を優先し、書込処理が投げたファイル操作以外の例外はそのまま伝える。
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
    /// <param name="path">読み取る保存済みJSONファイル</param>
    /// <returns>型付きの結果、元のバイト列、記録の完全性に関する問題を保持する記録 問題があっても読取可能なら返す</returns>
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
    /// <param name="path">読み取る変換manifestのJSONファイル</param>
    /// <returns>manifest、元のバイト列、記録の完全性に関する問題を保持する記録</returns>
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
    /// <param name="path">読み取る実行結果のJSONファイル</param>
    /// <returns>実行結果、元のバイト列、記録の完全性に関する問題を保持する記録</returns>
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
    /// <param name="manifest">記録内容の完全性を検証する変換結果</param>
    /// <returns>入力集合、未処理、完了状態、集計に関する問題一覧 問題がない場合は空</returns>
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
    /// <param name="report">記録内容の完全性を検証する実行結果</param>
    /// <returns>素材・実行結果の入力集合、commandの記録、完了状態、集計に関する問題一覧 問題がない場合は空</returns>
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

    /// <summary>
    /// 一時ファイルの削除を試み、保存処理の結果を削除失敗で置き換えない。
    /// </summary>
    /// <param name="path">削除する一時ファイルのpath 存在しない場合も受け付ける</param>
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

    /// <summary>
    /// 対応するschemaの保存結果から、manifestまたは実行結果の種類を読み取る。
    /// </summary>
    /// <param name="root">保存JSONのルート要素</param>
    /// <param name="path">読取失敗の診断に含めるファイルのpath</param>
    /// <returns>corpus_manifestまたはrun_report</returns>
    /// <exception cref="ReportStoreException">ルートがobjectでない場合、schemaの版が対応しない場合、またはkindが受け付けられない場合</exception>
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

    /// <summary>
    /// 未知の項目、必須項目の欠落、不正な分類、一覧内のnullを拒否して保存記録を復元する。
    /// </summary>
    /// <typeparam name="T">保存記録のDTO型</typeparam>
    /// <param name="root">復元する保存JSONのルート要素</param>
    /// <returns>保存内容から復元したnullでない記録</returns>
    /// <exception cref="JsonException">JSONが記録の保存形式に従わない場合、または復元結果がnullの場合</exception>
    private static T Deserialize<T>(JsonElement root)
    {
        EnsureNoNullElements(root);
        return root.Deserialize<T>(readOptions_) ?? throw new JsonException("結果がnullです。");
    }

    /// <summary>
    /// 入れ子を含むJSONの全配列に、nullの要素がないことを確認する。
    /// </summary>
    /// <param name="element">確認するJSON要素 objectのnull値は許容する</param>
    /// <exception cref="JsonException">配列にnullの要素がある場合</exception>
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

    /// <summary>
    /// ケースの入力pathが所属入力と一致し、command indexが負でないことを確認する。
    /// </summary>
    /// <param name="report">ケース識別を確認する実行結果</param>
    /// <param name="path">読取失敗の診断に含めるJSONファイルのpath</param>
    /// <exception cref="ReportStoreException">ケース識別の入力pathが異なる場合、またはcommand indexが負の場合</exception>
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

    /// <summary>
    /// JSON素材の列挙が完了し、保存された一覧と一致するcommand件数だけを取得する。
    /// </summary>
    /// <param name="input">基準となるJSON素材を含む入力の変換結果</param>
    /// <returns>JSON素材がちょうど1件あり、列挙完了と件数の一致を確認できる場合は件数、それ以外はnull</returns>
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

    /// <summary>
    /// profileの対象入力に対する欠落、重複、対象外の記録を問題一覧へ追加する。
    /// </summary>
    /// <param name="expected">profileの対象入力一覧</param>
    /// <param name="actual">記録された入力pathの一覧 大文字と小文字を区別して照合する</param>
    /// <param name="issues">問題の追加先</param>
    /// <param name="prefix">診断メッセージでpathの前に付ける対象の説明</param>
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

    /// <summary>
    /// 入力の処理状態、command件数、ケースの欠落・重複・範囲、分類の整合性を問題一覧へ追加する。
    /// </summary>
    /// <param name="input">検証する一つの入力の実行記録</param>
    /// <param name="materialCommandCount">素材から確定できたcommand件数 確定できない場合はnull</param>
    /// <param name="issues">問題の追加先</param>
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

    /// <summary>
    /// 処理・出力の未完了、または処理完了の記録と未処理対象の矛盾を問題一覧へ追加する。
    /// </summary>
    /// <param name="processingComplete">記録された処理完了の状態</param>
    /// <param name="outputComplete">記録された出力完了の状態</param>
    /// <param name="hasUnprocessed">記録内容に未処理の対象があるかどうか</param>
    /// <param name="issues">問題の追加先</param>
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
