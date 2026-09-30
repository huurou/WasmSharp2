using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 保存済みの変換条件・素材・ケース結果を比較し、差分と未比較の理由を返す
/// </summary>
internal static class BaselineComparer
{
    /// <summary>
    /// profileと素材同一性が一致する場合に、ケースの前後差とpassedからの回帰を比較する。
    /// </summary>
    /// <param name="baseline">比較基準となる保存済みの実行結果</param>
    /// <param name="current">比較する現在の実行結果</param>
    /// <returns>前後のケース詳細、差分、回帰、比較できなかった理由と完了状態を持つ比較結果</returns>
    internal static ComparisonReport CompareRun(RunReport baseline, RunReport current)
    {
        var report = CompareCorpus(baseline.Corpus, current.Corpus, ComparisonType.Run);
        report = report with
        {
            Baseline = new(string.Empty) { RunId = baseline.Provenance.RunId },
            Current = new(string.Empty) { RunId = current.Provenance.RunId },
            Established =
                report.ConditionDifferences.Count == 0
                && report.EntryDifferences.Count == 0
                && report.Uncompared.Count == 0,
        };
        AddRecordIssues(report, "baseline", ReportStore.Validate(baseline));
        AddRecordIssues(report, "current", ReportStore.Validate(current));
        if (report.Established)
        {
            CompareCases(report, baseline, current);
        }
        else
        {
            report.Uncompared.AddRange(
                report.ConditionDifferences.Select(x => new UncomparedItem(
                    x.Name,
                    "比較成立の条件が一致しないため、ケースの回帰を判定できません。"
                ))
            );
            report.Uncompared.AddRange(
                report.EntryDifferences.Select(x => new UncomparedItem(
                    x.Path,
                    $"{x.Property}が一致しないため、ケースの回帰を判定できません。"
                ))
            );
        }

        var cases = current.Inputs.SelectMany(x => x.Cases).ToArray();
        return Summarize(
            report,
            cases.Count(x => x.Outcome == CaseOutcome.Failed),
            cases.Count(x => x.Outcome == CaseOutcome.RunnerError)
                + current.Inputs.Sum(x => x.Issues.Count)
                + current.Diagnostics.Count
        );
    }

    /// <summary>
    /// ケース識別ごとに前後の詳細を対応付け、追加・欠落・重複を区別して記録する。
    /// </summary>
    /// <param name="report">ケース比較と比較できなかった理由を追加する結果</param>
    /// <param name="baseline">比較基準となる実行結果</param>
    /// <param name="current">比較する現在の実行結果</param>
    private static void CompareCases(ComparisonReport report, RunReport baseline, RunReport current)
    {
        var before = baseline.Inputs.SelectMany(x => x.Cases).ToLookup(x => x.Id);
        var after = current.Inputs.SelectMany(x => x.Cases).ToLookup(x => x.Id);
        foreach (
            var id in before
                .Select(x => x.Key)
                .Union(after.Select(x => x.Key))
                .OrderBy(x => x.InputPath, StringComparer.Ordinal)
                .ThenBy(x => x.CommandIndex)
        )
        {
            var left = before[id].ToArray();
            var right = after[id].ToArray();
            if (left.Length > 1 || right.Length > 1)
            {
                report.Uncompared.Add(
                    new(
                        id.ToString(),
                        $"ケース識別が重複しているため対応できません。baseline={left.Length}件、current={right.Length}件"
                    )
                );
                continue;
            }

            var baselineCase = left.Length == 1 ? CopyCase(left[0]) : null;
            var currentCase = right.Length == 1 ? CopyCase(right[0]) : null;
            var change = (baselineCase, currentCase) switch
            {
                (null, _) => CaseChange.Added,
                (_, null) => CaseChange.Missing,
                _ => JsonElement.DeepEquals(
                    JsonSerializer.SerializeToElement(baselineCase),
                    JsonSerializer.SerializeToElement(currentCase)
                )
                    ? CaseChange.Unchanged
                    : CaseChange.Changed,
            };
            report.Cases.Add(
                new(
                    id,
                    change,
                    baselineCase?.Outcome == CaseOutcome.Passed
                        && currentCase?.Outcome != CaseOutcome.Passed
                )
                {
                    Baseline = baselineCase,
                    Current = currentCase,
                }
            );
            if (baselineCase is null || currentCase is null)
            {
                report.Uncompared.Add(
                    new(
                        id.ToString(),
                        $"{(baselineCase is null ? "baseline" : "current")}に結果がないため、前後を対応付けられません。"
                    )
                );
            }
        }
    }

    /// <summary>
    /// 元のケースと可変一覧を共有しない、比較結果用のケース記録を作る。
    /// </summary>
    /// <param name="item">複製するケース結果</param>
    /// <returns>期待値のレーン、診断、出力、依存原因の一覧も独立させたケース結果</returns>
    private static CaseResult CopyCase(CaseResult item)
    {
        return item with
        {
            ExpectedValues = [.. item.ExpectedValues.Select(x => x with { Lanes = [.. x.Lanes] })],
            ActualValues = [.. item.ActualValues],
            Diagnostics =
            [
                .. item.Diagnostics.Select(x =>
                    x with
                    {
                        UnverifiedRanges = [.. x.UnverifiedRanges],
                    }
                ),
            ],
            Prints = [.. item.Prints.Select(x => x with { Arguments = [.. x.Arguments] })],
            Cause = item.Cause is { } cause
                ? cause with
                {
                    Direct = [.. cause.Direct],
                    Origins = [.. cause.Origins],
                }
                : null,
        };
    }

    /// <summary>
    /// 配置先や日時を同一性に含めず、変換条件・入力・生成物・変換状態を比較する。
    /// </summary>
    /// <param name="baseline">比較基準となる変換manifest</param>
    /// <param name="current">比較する現在の変換manifest</param>
    /// <returns>条件、素材、変換状態、参考出典の差と比較できなかった理由を持つ比較結果</returns>
    internal static ComparisonReport CompareConversion(
        CorpusManifest baseline,
        CorpusManifest current
    )
    {
        var report = CompareCorpus(baseline, current, ComparisonType.Conversion);
        AddRecordIssues(report, "baseline", ReportStore.Validate(baseline));
        AddRecordIssues(report, "current", ReportStore.Validate(current));
        return Summarize(
            report,
            currentFailedCount: 0,
            currentRunnerErrorCount: current.Inputs.Count(x =>
                x.Status == ConversionStatus.RunnerError
            ) + current.Diagnostics.Count
        );
    }

    /// <summary>
    /// 両manifestの固定条件と素材を比較し、出典差と対応できない対象も保持する。
    /// </summary>
    /// <param name="baseline">比較基準となるmanifest</param>
    /// <param name="current">比較する現在のmanifest</param>
    /// <param name="comparison">変換状態を比較に含めるかどうかを決める比較の種類</param>
    /// <returns>条件と素材の差を保持する、ケース比較と集計前の結果</returns>
    private static ComparisonReport CompareCorpus(
        CorpusManifest baseline,
        CorpusManifest current,
        ComparisonType comparison
    )
    {
        var report = new ComparisonReport(comparison, new(string.Empty), new(string.Empty))
        {
            Established = true,
        };
        CompareProfile(report, baseline.Profile, current.Profile);
        AddDifference(
            report.ProvenanceDifferences,
            "executable_sha256",
            baseline.Provenance.ExecutableSha256,
            current.Provenance.ExecutableSha256
        );

        foreach (
            var (path, before, after) in PairByName(
                baseline.Profile.Inputs,
                current.Profile.Inputs,
                x => x.Path,
                report.Uncompared,
                "profileの入力"
            )
        )
        {
            AddEntryDifference(report, path, "profile_input.sha256", before?.Sha256, after?.Sha256);
        }

        foreach (
            var (path, before, after) in PairByName(
                baseline.Inputs,
                current.Inputs,
                x => x.Input.Path,
                report.Uncompared,
                "入力の変換記録"
            )
        )
        {
            AddEntryDifference(
                report,
                path,
                "input.sha256",
                before?.Input.Sha256,
                after?.Input.Sha256
            );
            if (comparison == ComparisonType.Conversion)
            {
                AddEntryDifference(
                    report,
                    path,
                    "conversion.status",
                    before?.Status,
                    after?.Status
                );
            }
        }

        foreach (
            var (path, before, after) in PairByName(
                baseline.Inputs.SelectMany(x => x.Artifacts),
                current.Inputs.SelectMany(x => x.Artifacts),
                x => x.Path,
                report.Uncompared,
                "生成物"
            )
        )
        {
            AddEntryDifference(report, path, "artifact.sha256", before?.Sha256, after?.Sha256);
            AddEntryDifference(report, path, "artifact.kind", before?.Kind, after?.Kind);
            AddEntryDifference(
                report,
                path,
                "artifact.input_path",
                before?.InputPath,
                after?.InputPath
            );
        }

        return report;
    }

    /// <summary>
    /// profileの識別・取得元・commit・全feature・論理引数・変換optionを比較する。
    /// </summary>
    /// <param name="report">条件差と重複の理由を追加する比較結果</param>
    /// <param name="baseline">比較基準となるprofileのスナップショット</param>
    /// <param name="current">比較する現在のprofileのスナップショット</param>
    private static void CompareProfile(
        ComparisonReport report,
        ProfileSnapshot baseline,
        ProfileSnapshot current
    )
    {
        var differences = report.ConditionDifferences;
        AddDifference(differences, "profile.id", baseline.Id, current.Id);
        AddDifference(differences, "spec.url", baseline.Spec.Url, current.Spec.Url);
        AddDifference(differences, "spec.commit", baseline.Spec.Commit, current.Spec.Commit);
        AddDifference(differences, "wabt.url", baseline.Wabt.Url, current.Wabt.Url);
        AddDifference(differences, "wabt.commit", baseline.Wabt.Commit, current.Wabt.Commit);
        AddDifference(
            differences,
            "working_directory",
            baseline.WorkingDirectory,
            current.WorkingDirectory
        );
        AddDifference(
            differences,
            "conversion.check",
            baseline.Conversion.Check,
            current.Conversion.Check
        );
        AddDifference(
            differences,
            "conversion.canonical_lebs",
            baseline.Conversion.CanonicalLebs,
            current.Conversion.CanonicalLebs
        );
        AddDifference(
            differences,
            "conversion.relocatable",
            baseline.Conversion.Relocatable,
            current.Conversion.Relocatable
        );
        AddDifference(
            differences,
            "conversion.debug_names",
            baseline.Conversion.DebugNames,
            current.Conversion.DebugNames
        );
        if (
            !baseline.LogicalArguments.SequenceEqual(
                current.LogicalArguments,
                StringComparer.Ordinal
            )
        )
        {
            AddDifference(
                differences,
                "logical_arguments",
                Format(baseline.LogicalArguments),
                Format(current.LogicalArguments)
            );
        }

        foreach (
            var (name, before, after) in PairByName(
                baseline.Features,
                current.Features,
                x => x.Name,
                report.Uncompared,
                "feature"
            )
        )
        {
            AddDifference(
                differences,
                $"features.{name}.default_enabled",
                before?.DefaultEnabled,
                after?.DefaultEnabled
            );
            AddDifference(differences, $"features.{name}.enabled", before?.Enabled, after?.Enabled);
        }
    }

    /// <summary>
    /// 名前が一意な対象だけをOrdinal順に対応付け、重複した箇所は理由を残す。
    /// </summary>
    /// <typeparam name="T">名前で対応付ける記録の型</typeparam>
    /// <param name="baseline">比較基準の記録一覧</param>
    /// <param name="current">現在の記録一覧</param>
    /// <param name="getName">大文字と小文字を区別して照合する名前の取得処理</param>
    /// <param name="uncompared">列挙時に見つかった重複の理由を追加する一覧</param>
    /// <param name="kind">重複の診断に使用する対象の種類</param>
    /// <returns>名前順の対応一覧 片側にない記録はnullとし、どちらかで重複する名前は除く</returns>
    private static IEnumerable<(string Name, T? Baseline, T? Current)> PairByName<T>(
        IEnumerable<T> baseline,
        IEnumerable<T> current,
        Func<T, string> getName,
        List<UncomparedItem> uncompared,
        string kind
    )
        where T : class
    {
        var before = baseline.ToLookup(getName, StringComparer.Ordinal);
        var after = current.ToLookup(getName, StringComparer.Ordinal);
        foreach (
            var name in before
                .Select(x => x.Key)
                .Union(after.Select(x => x.Key), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        )
        {
            var left = before[name].ToArray();
            var right = after[name].ToArray();
            if (left.Length > 1 || right.Length > 1)
            {
                uncompared.Add(
                    new(
                        name,
                        $"{kind}が重複しているため対応できません。baseline={left.Length}件、current={right.Length}件"
                    )
                );
                continue;
            }

            yield return (name, left.SingleOrDefault(), right.SingleOrDefault());
        }
    }

    /// <summary>
    /// 同一性の条件または参考出典の値が異なる場合に、前後の値を表示用の文字列で記録する。
    /// </summary>
    /// <typeparam name="T">比較する値の型</typeparam>
    /// <param name="differences">差を追加する一覧</param>
    /// <param name="name">比較条件または参考出典の識別名</param>
    /// <param name="baseline">比較基準の値</param>
    /// <param name="current">現在の値</param>
    private static void AddDifference<T>(
        List<ConditionDifference> differences,
        string name,
        T baseline,
        T current
    )
    {
        if (!EqualityComparer<T>.Default.Equals(baseline, current))
        {
            differences.Add(new(name, Format(baseline), Format(current)));
        }
    }

    /// <summary>
    /// 入力または生成物の属性差を、片側だけ存在する場合も含めて記録する。
    /// </summary>
    /// <typeparam name="T">比較する属性値の型</typeparam>
    /// <param name="report">素材の属性差を追加する比較結果</param>
    /// <param name="path">入力または生成物を識別する相対path</param>
    /// <param name="property">比較する属性の識別名</param>
    /// <param name="baseline">比較基準の属性値</param>
    /// <param name="current">現在の属性値</param>
    private static void AddEntryDifference<T>(
        ComparisonReport report,
        string path,
        string property,
        T baseline,
        T current
    )
    {
        if (!EqualityComparer<T>.Default.Equals(baseline, current))
        {
            report.EntryDifferences.Add(new(path, property, Format(baseline), Format(current)));
        }
    }

    /// <summary>
    /// 比較する値をJSON要素の表示用文字列へ変換する。
    /// </summary>
    /// <typeparam name="T">変換する値の型</typeparam>
    /// <param name="value">比較結果に記録する値</param>
    /// <returns>nullはnull、文字列は引用符を除いた内容、それ以外はJSON要素の文字列表現</returns>
    private static string? Format<T>(T value)
    {
        return value is null ? null : JsonSerializer.SerializeToElement(value).ToString();
    }

    /// <summary>
    /// 保存契約の完全性検証で見つかった問題を、比較側の識別とともに保持する。
    /// </summary>
    /// <param name="report">比較できなかった理由を追加する結果</param>
    /// <param name="target">問題のある比較側を表すbaselineまたはcurrentの識別名</param>
    /// <param name="issues">保存済み記録の検証で見つかった問題一覧</param>
    private static void AddRecordIssues(
        ComparisonReport report,
        string target,
        IEnumerable<RecordIssue> issues
    )
    {
        report.Uncompared.AddRange(issues.Select(x => new UncomparedItem(target, x.Message)));
    }

    /// <summary>
    /// 差分と未比較の一覧から集計を求め、全対象を比較できた場合だけ完了とする。
    /// </summary>
    /// <param name="report">差分と比較できなかった理由を記録済みの比較結果</param>
    /// <param name="currentFailedCount">現在の実行結果に含まれるfailedの件数 変換比較では0</param>
    /// <param name="currentRunnerErrorCount">現在の結果に含まれるケース、入力、操作全体のrunner_errorの件数</param>
    /// <returns>集計と完了状態を更新した比較結果 差分やケースの一覧は入力結果と共有する</returns>
    private static ComparisonReport Summarize(
        ComparisonReport report,
        int currentFailedCount,
        int currentRunnerErrorCount
    )
    {
        return report with
        {
            Complete = report.Established && report.Uncompared.Count == 0,
            Summary = new(
                report.ConditionDifferences.Count,
                report.ProvenanceDifferences.Count,
                report.EntryDifferences.Count,
                report.Cases.Count(x => x.Change == CaseChange.Changed),
                report.Cases.Count(x => x.Change == CaseChange.Added),
                report.Cases.Count(x => x.Change == CaseChange.Missing),
                report.Cases.Count(x => x.Regression),
                report.Uncompared.Count,
                currentFailedCount,
                currentRunnerErrorCount
            ),
        };
    }
}
