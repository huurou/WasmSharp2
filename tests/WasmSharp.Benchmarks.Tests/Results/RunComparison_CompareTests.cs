using System.Security.Cryptography;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunComparison_CompareTests
{
    [Test]
    [Arguments(null, null)]
    [Arguments(16384, 1)]
    [Arguments(1, 1)]
    public async Task 子processの実行位置が欠落または不一致_数値目標を満たしても受入を阻害する(
        long? before,
        long? after
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        fixture.Current[0].AffinityBefore = before;
        fixture.Current[0].AffinityAfter = after;
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.IsAccepted).IsFalse();
            await Assert.That(report.Cases.Single().BlocksCompletion).IsTrue();
            await Assert
                .That(report.Cases.Single().Decision)
                .IsEqualTo(ComparisonDecision.Indeterminate);
            await Assert
                .That(
                    report
                        .Cases.Single()
                        .Reasons.Any(x => x.Kind == ComparisonReasonKind.ConditionMismatch)
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 時間が同等で割当目標だけを達成する_各runの値と中央値と差を示して改善として受け入れる()
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.IsAccepted).IsTrue();
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Improved);
            await Assert.That(actual.TargetAchieved).IsTrue();
            await Assert.That(actual.BlocksCompletion).IsFalse();
            await Assert.That(actual.Time.Baseline!.Median).IsEqualTo(100);
            await Assert.That(actual.Time.Baseline.Minimum).IsEqualTo(99);
            await Assert.That(actual.Time.Baseline.Maximum).IsEqualTo(101);
            await Assert
                .That(actual.Time.Baseline.Runs.Select(x => x.Value))
                .IsEquivalentTo([101d, 100d, 99d]);
            await Assert
                .That(actual.Time.Baseline.Runs[0].ConfidenceInterval)
                .IsEqualTo(new ConfidenceInterval(100.5, 101.5, 0.999));
            await Assert.That(actual.Time.Current!.Median).IsEqualTo(99);
            await Assert.That(actual.Time.Change).IsEqualTo(new MetricChange(-1, 0.99));
            await Assert.That(actual.Allocation.Change).IsEqualTo(new MetricChange(-4, 0.5));
            await Assert
                .That(actual.Allocation.Current!.Runs.All(x => x.ConfidenceInterval is null))
                .IsTrue();
        }
    }

    [Test]
    [Arguments("allowed-exceeded", ComparisonDecision.Regressed, false, true)]
    [Arguments("allowed-boundary", ComparisonDecision.Improved, true, false)]
    [Arguments("equivalence-increase", ComparisonDecision.Equivalent, false, false)]
    [Arguments("equivalence-decrease", ComparisonDecision.Equivalent, false, false)]
    [Arguments("outside-equivalence", ComparisonDecision.Indeterminate, false, false)]
    [Arguments("target-shortfall", ComparisonDecision.Indeterminate, false, false)]
    [Arguments("noise-boundary", ComparisonDecision.Indeterminate, false, false)]
    [Arguments("beyond-noise", ComparisonDecision.Improved, true, false)]
    [Arguments("untargeted", ComparisonDecision.Improved, false, false)]
    [Arguments("untargeted-noise", ComparisonDecision.Indeterminate, false, false)]
    [Arguments("other-metric-only", ComparisonDecision.Indeterminate, false, false)]
    [Arguments("time-target", ComparisonDecision.Improved, true, false)]
    [Arguments("ratio-target", ComparisonDecision.Improved, true, false)]
    public async Task 事前policyの境界を比較する_判定順序と目標対象と完了阻害を区別する(
        string scenario,
        ComparisonDecision expected,
        bool targetAchieved,
        bool blocks
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        SetGroup(fixture.Baseline, 100, 8);
        SetGroup(fixture.Current, 100, 8);
        var policy = fixture.Policy.Cases.Single();
        switch (scenario)
        {
            case "allowed-exceeded":
                SetGroup(fixture.Current, 106, 4);
                break;
            case "allowed-boundary":
                SetGroup(fixture.Current, 105, 4);
                break;
            case "equivalence-increase":
                SetGroup(fixture.Current, 102, 8);
                break;
            case "equivalence-decrease":
                SetGroup(fixture.Current, 98, 8);
                break;
            case "outside-equivalence":
                SetGroup(fixture.Current, 103, 8);
                break;
            case "target-shortfall":
                SetGroup(fixture.Current, 100, 5);
                break;
            case "noise-boundary":
                SetGroup(fixture.Current, 100, 4);
                policy = policy with
                {
                    Allocation = policy.Allocation with { Noise = new AbsoluteThreshold(4) },
                };
                break;
            case "beyond-noise":
                SetGroup(fixture.Current, 100, 4);
                policy = policy with
                {
                    Allocation = policy.Allocation with { Noise = new AbsoluteThreshold(3.999) },
                };
                break;
            case "untargeted":
            case "untargeted-noise":
                SetGroup(fixture.Current, 97, 8);
                policy = policy with
                {
                    Time = policy.Time with
                    {
                        Noise = new AbsoluteThreshold(scenario == "untargeted" ? 0.5 : 3),
                    },
                    Allocation = policy.Allocation with { ReductionTarget = null },
                };
                break;
            case "other-metric-only":
                SetGroup(fixture.Current, 100, 0);
                policy = policy with
                {
                    Time = policy.Time with { ReductionTarget = new AbsoluteThreshold(10) },
                    Allocation = policy.Allocation with { ReductionTarget = null },
                };
                break;
            case "time-target":
            case "ratio-target":
                SetGroup(fixture.Current, 90, 8);
                policy = policy with
                {
                    Time =
                        scenario == "time-target"
                            ? policy.Time with
                            {
                                ReductionTarget = new AbsoluteThreshold(10),
                            }
                            : new(
                                new RatioThreshold(0.05),
                                new RatioThreshold(0.1),
                                new(new RatioThreshold(0.02), new RatioThreshold(0.02)),
                                new RatioThreshold(0.005),
                                new RatioThreshold(0.02)
                            ),
                    Allocation = policy.Allocation with { ReductionTarget = null },
                };
                break;
        }
        fixture.Policy = fixture.Policy with { Cases = [policy] };
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(expected);
            await Assert.That(actual.TargetAchieved).IsEqualTo(targetAchieved);
            await Assert.That(actual.BlocksCompletion).IsEqualTo(blocks);
            await Assert.That(report.IsAccepted).IsEqualTo(targetAchieved && !blocks);
            await Assert.That(actual.Reasons).IsNotEmpty();
        }
    }

    [Test]
    public async Task 割当の表示値が両群とも0である_丸め前の絶対差で悪化を判定して零基準の比率をnullにする()
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        SetGroup(fixture.Baseline, 100, 0);
        SetGroup(fixture.Current, 100, 1d / 4096);
        var policy = fixture.Policy.Cases.Single();
        fixture.Policy = fixture.Policy with
        {
            Cases =
            [
                policy with
                {
                    Allocation = new(
                        new AbsoluteThreshold(0),
                        null,
                        new(new AbsoluteThreshold(0), new AbsoluteThreshold(0)),
                        new AbsoluteThreshold(0),
                        new AbsoluteThreshold(0)
                    ),
                },
            ],
        };
        var request = fixture.Save();

        // Act
        var actual = RunComparison.Compare(request).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Regressed);
            await Assert
                .That(actual.Allocation.Change)
                .IsEqualTo(new MetricChange(1d / 4096, null));
            await Assert.That(actual.Allocation.Current!.Median).IsEqualTo(1d / 4096);
            await Assert.That(actual.Allocation.WithinAllowedIncrease).IsFalse();
        }
    }

    [Test]
    public async Task 零基準に比率閾値しかない_絶対幅を補完せずpolicy不足として完了を阻害する()
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        SetGroup(fixture.Baseline, 100, 0);
        SetGroup(fixture.Current, 100, 1d / 4096);
        var policy = fixture.Policy.Cases.Single();
        fixture.Policy = fixture.Policy with
        {
            Cases =
            [
                policy with
                {
                    Allocation = policy.Allocation with
                    {
                        AllowedIncrease = new RatioThreshold(0.02),
                        Equivalence = new(new AbsoluteThreshold(0), new RatioThreshold(0.02)),
                        RunVariation = new RatioThreshold(0.02),
                    },
                },
            ],
        };
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Indeterminate);
            await Assert.That(actual.BlocksCompletion).IsTrue();
            await Assert.That(actual.Allocation.WithinAllowedIncrease).IsNull();
            await Assert
                .That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.MissingPolicy))
                .IsTrue();
            await Assert.That(report.IsAccepted).IsFalse();
        }
    }

    [Test]
    [Arguments("current-condition", ComparisonReasonKind.ConditionMismatch)]
    [Arguments("baseline-condition", ComparisonReasonKind.ConditionMismatch)]
    [Arguments("failed", ComparisonReasonKind.CorrectnessFailure)]
    [Arguments("failure-record", ComparisonReasonKind.CorrectnessFailure)]
    [Arguments("incomplete", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("smoke", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("diagnostic", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("retention", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("missing-case", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("too-few", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("empty-current", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("duplicate", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("shared", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("corrupt", ComparisonReasonKind.InsufficientMeasurements)]
    [Arguments("missing-run", ComparisonReasonKind.InsufficientMeasurements)]
    public async Task 比較の前提が成立しない_数値の改善や悪化に優先して理由付きで完了を阻害する(
        string scenario,
        ComparisonReasonKind expectedReason
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        SetGroup(fixture.Current, 110, 4);
        var changed = fixture.Current[2];
        switch (scenario)
        {
            case "current-condition":
                changed.Environment = changed.Environment with { Cpu = "different CPU" };
                break;
            case "baseline-condition":
                fixture.Baseline[1].Environment = fixture.Baseline[1].Environment with
                {
                    Cpu = "different CPU",
                };
                break;
            case "failed":
                changed.Manifest = changed.Manifest with { Status = ResultStatus.Failed };
                break;
            case "failure-record":
                changed.Manifest = changed.Manifest with
                {
                    Failures =
                    [
                        new(
                            "invoke.single",
                            RunStage.Postcheck,
                            RunStage.ExecutionStarted,
                            "固定期待値に不一致",
                            null,
                            null,
                            null
                        ),
                    ],
                };
                break;
            case "incomplete":
                changed.Manifest = changed.Manifest with { Status = ResultStatus.Incomplete };
                break;
            case "smoke":
                changed.Manifest = changed.Manifest with { Kind = RunKind.Smoke };
                break;
            case "diagnostic":
                changed.Manifest = changed.Manifest with { Kind = RunKind.Diagnostic };
                break;
            case "retention":
                changed.Manifest = changed.Manifest with { Kind = RunKind.Retention };
                break;
            case "missing-case":
                changed.SetCases([]);
                break;
        }
        var request = fixture.Save();
        request = scenario switch
        {
            "too-few" => request with
            {
                CurrentRunDirectories = request.CurrentRunDirectories.Take(2).ToList(),
            },
            "empty-current" => request with { CurrentRunDirectories = [] },
            "duplicate" => request with
            {
                CurrentRunDirectories =
                [
                    .. request.CurrentRunDirectories,
                    request.CurrentRunDirectories[0],
                ],
            },
            "shared" => request with
            {
                CurrentRunDirectories = request.BaselineRunDirectories.ToList(),
            },
            "missing-run" => request with
            {
                CurrentRunDirectories =
                [
                    .. request.CurrentRunDirectories,
                    Path.Combine(changed.DirectoryPath, "absent"),
                ],
            },
            _ => request,
        };
        if (scenario == "corrupt")
        {
            File.WriteAllText(Path.Combine(changed.DirectoryPath, "raw/full.json"), "changed");
        }

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Indeterminate);
            await Assert.That(actual.BlocksCompletion).IsTrue();
            await Assert.That(actual.TargetAchieved).IsFalse();
            await Assert.That(actual.Reasons.Any(x => x.Kind == expectedReason)).IsTrue();
            await Assert.That(report.IsAccepted).IsFalse();
        }
    }

    [Test]
    [Arguments("time")]
    [Arguments("allocation")]
    [Arguments("gc")]
    public async Task 必須指標を取得できない_零補完せず元の取得不能理由を報告する(string metric)
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var run = fixture.Current[0];
        var result = run.Manifest.Cases.Single();
        const string UNAVAILABLE = "固定fixtureの必須観測を取得できません";
        var metrics = metric switch
        {
            "time" => result.Metrics with { Time = null, TimeUnavailableReason = UNAVAILABLE },
            "allocation" => result.Metrics with
            {
                Allocation = null,
                AllocationUnavailableReason = UNAVAILABLE,
            },
            _ => result.Metrics with { Gc = null, GcUnavailableReason = UNAVAILABLE },
        };
        run.Manifest = run.Manifest with { Cases = [result with { Metrics = metrics }] };
        var request = fixture.Save();

        // Act
        var actual = RunComparison.Compare(request).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Indeterminate);
            await Assert.That(actual.BlocksCompletion).IsTrue();
            await Assert
                .That(
                    actual.Reasons.Any(x =>
                        x.Kind == ComparisonReasonKind.InsufficientMeasurements
                        && x.Description.Contains(UNAVAILABLE, StringComparison.Ordinal)
                    )
                )
                .IsTrue();
            if (metric == "time")
            {
                await Assert.That(actual.Time.Current!.Runs).Count().IsEqualTo(2);
            }
            if (metric == "allocation")
            {
                await Assert.That(actual.Allocation.Current!.Runs).Count().IsEqualTo(2);
            }
        }
    }

    [Test]
    [Arguments("missing")]
    [Arguments("duplicate")]
    [Arguments("minimum")]
    [Arguments("negative")]
    [Arguments("target-zero")]
    [Arguments("equivalence")]
    [Arguments("mixed-equivalence")]
    [Arguments("hash")]
    [Arguments("schema")]
    [Arguments("json")]
    [Arguments("null")]
    [Arguments("absent")]
    public async Task policyが不足または不整合である_基準を後付けせず理由を保持する(string scenario)
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var policy = fixture.Policy.Cases.Single();
        var time = policy.Time;
        switch (scenario)
        {
            case "missing":
                fixture.Policy = fixture.Policy with { Cases = [] };
                break;
            case "duplicate":
                fixture.Policy = fixture.Policy with { Cases = [policy, policy] };
                break;
            case "minimum":
                fixture.Policy = fixture.Policy with
                {
                    Cases = [policy with { MinimumIndependentRuns = 2 }],
                };
                break;
            case "negative":
                time = time with { AllowedIncrease = new AbsoluteThreshold(-1) };
                break;
            case "target-zero":
                time = time with { ReductionTarget = new AbsoluteThreshold(0) };
                break;
            case "equivalence":
                time = time with
                {
                    Equivalence = time.Equivalence with { Increase = new AbsoluteThreshold(6) },
                };
                break;
            case "mixed-equivalence":
                time = time with { AllowedIncrease = new RatioThreshold(0.01) };
                break;
            case "schema":
                fixture.Policy = fixture.Policy with { SchemaVersion = 2 };
                break;
        }
        if (time != policy.Time)
        {
            fixture.Policy = fixture.Policy with { Cases = [policy with { Time = time }] };
        }
        var request = fixture.Save();
        if (scenario is "json" or "null")
        {
            File.WriteAllText(request.PolicyPath, scenario == "null" ? "null" : "{}");
            request = request with
            {
                PolicySha256 = Convert.ToHexStringLower(
                    SHA256.HashData(File.ReadAllBytes(request.PolicyPath))
                ),
            };
        }
        if (scenario == "hash")
        {
            request = request with { PolicySha256 = "different-hash" };
        }
        if (scenario == "absent")
        {
            request = request with
            {
                PolicyPath = Path.Combine(fixture.Baseline[0].DirectoryPath, "absent.json"),
            };
        }

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Indeterminate);
            await Assert.That(actual.BlocksCompletion).IsTrue();
            await Assert
                .That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.MissingPolicy))
                .IsTrue();
            await Assert.That(report.IsAccepted).IsFalse();
        }
    }

    [Test]
    [Arguments("baseline-time", false)]
    [Arguments("baseline-time", true)]
    [Arguments("current-time", false)]
    [Arguments("current-time", true)]
    [Arguments("baseline-allocation", false)]
    [Arguments("baseline-allocation", true)]
    [Arguments("current-allocation", false)]
    [Arguments("current-allocation", true)]
    [Arguments("current-ratio", false)]
    [Arguments("current-ratio", true)]
    public async Task 群の全runを中央値からの判断幅で検証する_境界を含み超過時は悪化より先に判定不能とする(
        string scenario,
        bool exceeds
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        SetGroup(fixture.Baseline, 100, 8);
        SetGroup(fixture.Current, 100, 4);
        var policy = fixture.Policy.Cases.Single();
        var group = scenario.StartsWith("baseline", StringComparison.Ordinal)
            ? fixture.Baseline
            : fixture.Current;
        if (scenario.EndsWith("allocation", StringComparison.Ordinal))
        {
            var allocation = group == fixture.Baseline ? 8 : 4;
            ComparisonFixture.SetValues(group[0], 100, allocation - 0.5);
            ComparisonFixture.SetValues(group[2], 100, allocation + (exceeds ? 0.75 : 0.5));
            policy = policy with
            {
                Allocation = policy.Allocation with { RunVariation = new AbsoluteThreshold(0.5) },
            };
            if (exceeds)
            {
                SetGroup(fixture.Current, 110, 4);
                if (group == fixture.Current)
                {
                    ComparisonFixture.SetValues(group[2], 110, 4.75);
                }
            }
        }
        else if (scenario == "current-ratio")
        {
            SetGroup(fixture.Current, 50, 4);
            ComparisonFixture.SetValues(group[0], 49, 4);
            ComparisonFixture.SetValues(group[2], exceeds ? 51.125 : 51, 4);
            policy = policy with
            {
                Time = policy.Time with { RunVariation = new RatioThreshold(0.02) },
            };
        }
        else
        {
            ComparisonFixture.SetValues(group[0], 98, group == fixture.Baseline ? 8 : 4);
            ComparisonFixture.SetValues(
                group[2],
                exceeds ? 103 : 102,
                group == fixture.Baseline ? 8 : 4
            );
            if (exceeds && group == fixture.Baseline)
            {
                SetGroup(fixture.Current, 110, 4);
            }
        }
        fixture.Policy = fixture.Policy with { Cases = [policy] };
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);
        var actual = report.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(actual.Decision)
                .IsEqualTo(
                    exceeds ? ComparisonDecision.Indeterminate : ComparisonDecision.Improved
                );
            await Assert.That(actual.BlocksCompletion).IsEqualTo(exceeds);
            await Assert
                .That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.ExcessiveVariation))
                .IsEqualTo(exceeds);
            await Assert.That(report.IsAccepted).IsEqualTo(!exceeds);
        }
    }

    [Test]
    [Arguments("equivalent", true)]
    [Arguments("nonblocking", true)]
    [Arguments("untargeted", true)]
    [Arguments("regressed", false)]
    [Arguments("policy-missing", false)]
    [Arguments("target-missing", false)]
    public async Task 全15ケースの最終比較を行う_全指標の許容内と選定目標の再現を両方必要とする(
        string scenario,
        bool accepted
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture(allCases: true);
        SetGroup(fixture.Baseline, 100, 8);
        SetGroup(fixture.Current, 100, 8);
        fixture.Policy = fixture.Policy with
        {
            Cases = fixture
                .Policy.Cases.Select(x =>
                    x.CaseId == "invoke.single"
                        ? x
                        : x with
                        {
                            Allocation = x.Allocation with { ReductionTarget = null },
                        }
                )
                .ToList(),
        };
        var otherCase = fixture.CaseIds.Last(x => x != "invoke.single");
        foreach (var run in fixture.Current)
        {
            ComparisonFixture.SetValues(
                run,
                100,
                scenario == "target-missing" ? 5 : 4,
                "invoke.single"
            );
            ComparisonFixture.SetValues(
                run,
                scenario switch
                {
                    "nonblocking" => 103,
                    "untargeted" => 97,
                    "regressed" => 106,
                    _ => 100,
                },
                8,
                otherCase
            );
        }
        if (scenario == "policy-missing")
        {
            fixture.Policy = fixture.Policy with
            {
                Cases = fixture.Policy.Cases.Where(x => x.CaseId != otherCase).ToList(),
            };
        }
        var request = fixture.Save();

        // Act
        var report = RunComparison.Compare(request);
        var other = report.Cases.Single(x => x.CaseId == otherCase);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Cases).Count().IsEqualTo(15);
            await Assert.That(report.Cases.Select(x => x.CaseId)).IsEquivalentTo(fixture.CaseIds);
            await Assert
                .That(
                    report.Cases.All(x =>
                        x.Time.Baseline!.Runs.Count == 3 && x.Allocation.Current!.Runs.Count == 3
                    )
                )
                .IsTrue();
            await Assert.That(report.IsAccepted).IsEqualTo(accepted);
            await Assert
                .That(report.Cases.Count(x => x.TargetAchieved))
                .IsEqualTo(scenario == "target-missing" ? 0 : 1);
            await Assert
                .That(other.Decision)
                .IsEqualTo(
                    scenario switch
                    {
                        "nonblocking" or "policy-missing" => ComparisonDecision.Indeterminate,
                        "untargeted" => ComparisonDecision.Improved,
                        "regressed" => ComparisonDecision.Regressed,
                        _ => ComparisonDecision.Equivalent,
                    }
                );
            await Assert.That(other.TargetAchieved).IsFalse();
            await Assert
                .That(other.BlocksCompletion)
                .IsEqualTo(scenario is "regressed" or "policy-missing");
            if (scenario == "nonblocking")
            {
                await Assert
                    .That(
                        other.Reasons.Any(x =>
                            x.Kind == ComparisonReasonKind.OutsideEquivalenceWithinAllowance
                        )
                    )
                    .IsTrue();
            }
            if (scenario == "target-missing")
            {
                var selected = report.Cases.Single(x => x.CaseId == "invoke.single");
                await Assert.That(selected.BlocksCompletion).IsFalse();
                await Assert
                    .That(
                        selected.Reasons.Any(x => x.Kind == ComparisonReasonKind.TargetNotReached)
                    )
                    .IsTrue();
            }
        }
    }

    [Test]
    public async Task 変更ごとの悪化は許容内でも元の基準から累積する_最終比較で悪化を検出する()
    {
        // Arrange
        using var first = new ComparisonFixture();
        using var second = new ComparisonFixture();
        using var final = new ComparisonFixture();
        SetGroup(first.Baseline, 100, 8);
        SetGroup(first.Current, 104, 4);
        SetGroup(second.Baseline, 104, 4);
        SetGroup(second.Current, 108, 0);
        SetGroup(final.Baseline, 100, 8);
        SetGroup(final.Current, 108, 0);
        var firstRequest = first.Save();
        var secondRequest = second.Save();
        var finalRequest = final.Save();

        // Act
        var firstReport = RunComparison.Compare(firstRequest);
        var secondReport = RunComparison.Compare(secondRequest);
        var finalReport = RunComparison.Compare(finalRequest);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstReport.IsAccepted).IsTrue();
            await Assert.That(secondReport.IsAccepted).IsTrue();
            await Assert.That(finalReport.IsAccepted).IsFalse();
            await Assert
                .That(finalReport.Cases.Single().Decision)
                .IsEqualTo(ComparisonDecision.Regressed);
            await Assert
                .That(finalReport.Cases.Single().Time.Change)
                .IsEqualTo(new MetricChange(8, 1.08));
            await Assert
                .That(finalReport.Cases.Single().Allocation.Decision)
                .IsEqualTo(ComparisonDecision.Improved);
            await Assert.That(finalReport.Cases.Single().TargetAchieved).IsFalse();
        }
    }

    [Test]
    public async Task 独立runが偶数個ある_中央2値の平均を中央値として各runのCIをそのまま保持する()
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var before = new RunComparisonFixture("before-3", "D:/before");
        var after = new RunComparisonFixture("after-3", "E:/after");
        before.SetCases(fixture.Baseline[0].Cases);
        after.SetCases(fixture.Current[0].Cases);
        fixture.Baseline.Add(before);
        fixture.Current.Add(after);
        for (var index = 0; index < 4; index++)
        {
            ComparisonFixture.SetValues(fixture.Baseline[index], 100 + index * 10, 8);
            ComparisonFixture.SetValues(fixture.Current[index], 90 + index * 10, 8);
        }
        var policy = fixture.Policy.Cases.Single();
        fixture.Policy = fixture.Policy with
        {
            Cases =
            [
                policy with
                {
                    Time = policy.Time with
                    {
                        ReductionTarget = new AbsoluteThreshold(10),
                        RunVariation = new AbsoluteThreshold(20),
                    },
                    Allocation = policy.Allocation with { ReductionTarget = null },
                },
            ],
        };
        var request = fixture.Save();

        // Act
        var actual = RunComparison.Compare(request).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Decision).IsEqualTo(ComparisonDecision.Improved);
            await Assert.That(actual.Time.Baseline!.Median).IsEqualTo(115);
            await Assert.That(actual.Time.Current!.Median).IsEqualTo(105);
            await Assert
                .That(actual.Time.Baseline.Runs[3].ConfidenceInterval)
                .IsEqualTo(new ConfidenceInterval(129.5, 130.5, 0.999));
            await Assert.That(actual.Time.Change!.AbsoluteDifference).IsEqualTo(-10);
        }
    }

    [Test]
    public async Task 保存runを比較する_入力のIDとmanifestとpolicyのhashを対応付けファイルを変更しない()
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var request = fixture.Save();
        var before = FileHashes(fixture);

        // Act
        var report = RunComparison.Compare(request);
        var after = FileHashes(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(after).IsEquivalentTo(before);
            await Assert
                .That(report.BaselineRuns.Select(x => x.RunId))
                .IsEquivalentTo(["before-0", "before-1", "before-2"]);
            await Assert
                .That(report.CurrentRuns.Select(x => x.RunId))
                .IsEquivalentTo(["after-0", "after-1", "after-2"]);
            await Assert
                .That(report.Policy)
                .IsEqualTo(
                    new WasmSharp.Benchmarks.Cases.ManifestFile(
                        request.PolicyPath,
                        request.PolicySha256
                    )
                );
            foreach (var run in report.BaselineRuns.Concat(report.CurrentRuns))
            {
                await Assert.That(run.Manifest.Sha256).IsEqualTo(before[run.Manifest.RelativePath]);
            }
        }
    }

    private static Dictionary<string, string> FileHashes(ComparisonFixture fixture) =>
        fixture
            .Baseline.Concat(fixture.Current)
            .SelectMany(x => Directory.GetFiles(x.DirectoryPath, "*", SearchOption.AllDirectories))
            .ToDictionary(
                x => x,
                x => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x)))
            );

    private static void SetGroup(
        IEnumerable<RunComparisonFixture> runs,
        double time,
        double allocation
    )
    {
        foreach (var run in runs)
        {
            ComparisonFixture.SetValues(run, time, allocation);
        }
    }
}
