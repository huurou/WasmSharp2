using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunComparison_CompareConditionsTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();
    private readonly RunComparison comparison_;
    private readonly BenchmarkRun benchmarkRun_;

    public RunComparison_CompareConditionsTests()
    {
        comparison_ = new RunComparison(json_);
        benchmarkRun_ = new BenchmarkRun(json_, methods_);
    }

    [Test]
    [Arguments("WasmSharp.Benchmarks.dll", true, true)]
    [Arguments("WasmSharp.Benchmarks", true, true)]
    [Arguments("WasmSharp.Benchmarks.json", false, true)]
    [Arguments("Other.exe", false, true)]
    [Arguments("WasmSharp.Benchmarks.exe", false, false)]
    public async Task 起動ファイル名や拡張子が異なる_同じharnessの実行形式だけを同条件にする(
        string executable,
        bool comparable,
        bool hasHarnessAssembly
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        baseline.Environment = baseline.Environment with
        {
            Command = baseline.Environment.Command with
            {
                ExecutablePath = Path.Combine(baseline.RepositoryDirectory, "bin", "WasmSharp.Benchmarks.exe"),
            },
        };
        current.Environment = current.Environment with
        {
            Command = current.Environment.Command with
            {
                ExecutablePath = Path.Combine(current.RepositoryDirectory, "elsewhere", executable),
            },
        };
        if (!hasHarnessAssembly)
        {
            foreach (var fixture in new[] { baseline, current })
            {
                fixture.Source = fixture.Source with
                {
                    AssembliesBefore = [.. fixture.Source.AssembliesBefore.Where(x => x.Role != AssemblyRole.Harness)],
                    AssembliesAfter = [.. fixture.Source.AssembliesAfter.Where(x => x.Role != AssemblyRole.Harness)],
                };
            }
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsEqualTo(comparable);
            if (!comparable)
            {
                var difference = actual.Differences.Single();
                await Assert.That(difference.Condition).IsEqualTo("command.executable");
                await Assert
                    .That(difference.Baseline)
                    .IsEqualTo(hasHarnessAssembly ? "assembly:Harness" : "repository:bin/WasmSharp.Benchmarks.exe");
                await Assert.That(difference.Current).IsEqualTo("repository:elsewhere/" + executable);
            }
        }
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(true, 0)]
    public async Task 子processのreportまたは検査参照が欠ける_成功記録の指標だけで比較を受け入れない(
        bool missingReport,
        int launches
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Jobs = [current.Jobs.Single() with { LaunchCount = launches }];
        baseline.Save();
        current.Save();
        var manifest = RunStore.Load(current.DirectoryPath, json_);
        var result = manifest.Cases.Single();
        var report = result.BenchmarkReport!;
        var changed = manifest with
        {
            Cases =
            [
                result with
                {
                    BenchmarkReport = missingReport
                        ? null
                        : report with
                        {
                            Processes = [report.Processes.Single() with { CheckFile = null }],
                        },
                },
            ],
        };
        File.WriteAllBytes(
            Path.Combine(current.DirectoryPath, "run.json"),
            JsonSerializer.SerializeToUtf8Bytes(changed, json_.Options)
        );

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(actual.Reasons.Single().Kind).IsEqualTo(ComparisonReasonKind.ConditionMismatch);
            await Assert
                .That(actual.Reasons.Single().Description)
                .Contains(
                    launches == 0 ? current.DirectoryPath
                    : missingReport ? "全予定子processのaffinity観測が揃っていません"
                    : "子processのaffinity検査がありません"
                );
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 成功caseに対応する保存定義または素材がない_比較条件の読込失敗として報告する(bool missingAsset)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        if (missingAsset)
        {
            current.Assets = current.Assets with { Assets = [] };
        }
        else
        {
            current.Cases = [];
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var reason = actual.Reasons.Single();
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(reason.Kind).IsEqualTo(ComparisonReasonKind.ConditionMismatch);
            await Assert.That(reason.Description).Contains(current.DirectoryPath).And.Contains("比較条件を読めません");
        }
    }

    [Test]
    public async Task 同じJobの別caseで子processのaffinityが異なる_選択caseが一致していても比較を拒否する()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        var definition = baseline.Cases.Single();
        List<BenchmarkCase> cases = [definition, definition with { CaseId = "invoke.other" }];
        baseline.SetCases(cases);
        current.SetCases(cases);
        baseline.Jobs.Add(baseline.Jobs.Single() with { JobId = "secondary" });
        current.Jobs.Add(current.Jobs.Single() with { JobId = "secondary" });
        current.ProcessAffinities[(current.Jobs[0].JobId, "invoke.other")] = 1;
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(actual.Reasons.Single().Kind).IsEqualTo(ComparisonReasonKind.ConditionMismatch);
            await Assert.That(actual.Reasons.Single().Description).Contains("同じJobの子process間でaffinityが異なります");
        }
    }

    [Test]
    public async Task 配置先が違うtoolやrootやassemblyを渡す_同じ役割の引数を同条件として扱う()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        foreach (var fixture in new[] { baseline, current })
        {
            var root = fixture.RepositoryDirectory;
            var toolRoot = Path.Combine(Path.GetTempPath(), "sdk-layout-" + fixture.Manifest.RunId);
            fixture.Environment = fixture.Environment with
            {
                Command = fixture.Environment.Command with
                {
                    ExecutablePath = Path.Combine(toolRoot, fixture.Manifest.RunId == "before" ? "dotnet.exe" : "dotnet"),
                    Arguments =
                    [
                        .. fixture.Environment.Command.Arguments,
                        "--working-root=" + root,
                        "--runtime=" + fixture.Source.AssembliesBefore.Single(x => x.Role == AssemblyRole.Runtime).Path,
                        "--harness=" + fixture.Source.AssembliesBefore.Single(x => x.Role == AssemblyRole.Harness).Path,
                        "--settings=" + Path.Combine(root, "settings", "settings.json"),
                    ],
                },
                EnvironmentVariables =
                [
                    .. fixture.Environment.EnvironmentVariables,
                    new("DOTNET_ROOT", Path.Combine(toolRoot, "dotnet")),
                    new("DOTNET_ROOT_X64", Path.Combine(toolRoot, "dotnet-x64")),
                ],
            };
            fixture.Save();
        }

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsTrue();
            await Assert.That(actual.Differences).IsEmpty();
            await Assert.That(actual.Reasons).IsEmpty();
        }
    }

    [Test]
    public async Task 出力先と素材引数の表記が異なる_保存用の表記差を比較条件に含めない()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Environment = current.Environment with
        {
            Command = current.Environment.Command with
            {
                Arguments =
                [
                    .. current
                        .Environment.Command.Arguments.SkipLast(2)
                        .Select(x => x == "--asset-manifest" ? "--assets" : x),
                    "--output=" + current.Environment.Command.Arguments.Last(),
                ],
            },
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsTrue();
            await Assert.That(actual.Differences).IsEmpty();
            await Assert.That(actual.Reasons).IsEmpty();
        }
    }

    [Test]
    public async Task Repository名の接頭辞だけを共有する外部pathを渡す_未知のbuild引数の差を保持する()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        const string OPTION = "-p:DirectoryBuildPropsPath=";
        var before = baseline.RepositoryDirectory + "-copy/settings.props";
        var after = current.RepositoryDirectory + "-copy/settings.props";
        foreach (var fixture in new[] { baseline, current })
        {
            var path = fixture == baseline ? before : after;
            fixture.Environment = fixture.Environment with
            {
                Command = fixture.Environment.Command with
                {
                    Arguments =
                    [
                        .. fixture.Environment.Command.Arguments.Take(5),
                        OPTION + path,
                        .. fixture.Environment.Command.Arguments.Skip(5),
                    ],
                },
            };
            fixture.Save();
        }

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single();
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Condition).IsEqualTo("command.arguments[5]");
            await Assert.That(difference.Baseline).IsEqualTo(OPTION + before.Replace('\\', '/'));
            await Assert.That(difference.Current).IsEqualTo(OPTION + after.Replace('\\', '/'));
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 生成素材の引数を別pathで指定する_相対絶対の表記差だけを同じ素材として扱う(bool sameAsset)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        var asset = current.Assets.Assets.Single();
        var relative = asset.Sources.Single().RelativePath;
        var argument = sameAsset ? Path.Combine(current.RepositoryDirectory, "assets", relative) : "not-" + relative;
        var step = asset.GenerationSteps.Single();
        current.Assets = current.Assets with
        {
            Assets = [asset with { GenerationSteps = [step with { Arguments = [argument, .. step.Arguments.Skip(1)] }] }],
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsEqualTo(sameAsset);
            if (sameAsset)
            {
                await Assert.That(actual.Differences).IsEmpty();
                await Assert.That(actual.Reasons).IsEmpty();
            }
            else
            {
                var difference = actual.Differences.Single();
                await Assert.That(difference.Condition).IsEqualTo("asset.generation[0].arguments[0]");
                await Assert.That(difference.Baseline).IsEqualTo("asset:" + relative);
                await Assert.That(difference.Current).IsEqualTo(argument);
            }
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 素材参照の末尾が重なる_別のsourceとprofileまたはbinaryを同一視しない(bool profile)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        const string SHORT_PATH = "shared.bin";
        const string SOURCE_PATH = "nested/shared.bin";
        foreach (var fixture in new[] { baseline, current })
        {
            var asset = fixture.Assets.Assets.Single();
            fixture.Assets = fixture.Assets with
            {
                Profile = profile ? fixture.Assets.Profile with { RelativePath = SHORT_PATH } : fixture.Assets.Profile,
                Assets =
                [
                    asset with
                    {
                        Binary = profile ? asset.Binary : asset.Binary with { RelativePath = SHORT_PATH },
                        Sources = [asset.Sources.Single() with { RelativePath = SOURCE_PATH }],
                        GenerationSteps =
                        [
                            asset.GenerationSteps.Single() with
                            {
                                Arguments = [fixture == baseline ? SOURCE_PATH : SHORT_PATH],
                            },
                        ],
                    },
                ],
            };
            fixture.Save();
        }

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == "asset.generation[0].arguments[0]");
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo("asset:" + SOURCE_PATH);
            await Assert.That(difference.Current).IsEqualTo("asset:" + SHORT_PATH);
        }
    }

    [Test]
    [Arguments("root", "repository-root")]
    [Arguments("runtime", "assembly:Runtime")]
    [Arguments("harness", "assembly:Harness")]
    [Arguments("settings", "repository:settings/settings.json")]
    [Arguments("project", "project:tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj")]
    public async Task 役割を持つpathが空引数へ変わる_配置の正規化で意味のある差を消さない(string role, string expected)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        var value = role switch
        {
            "root" => baseline.RepositoryDirectory,
            "runtime" => baseline.Source.AssembliesBefore.Single(x => x.Role == AssemblyRole.Runtime).Path,
            "harness" => baseline.Source.AssembliesBefore.Single(x => x.Role == AssemblyRole.Harness).Path,
            "project" => Path.Combine(
                baseline.RepositoryDirectory,
                "tools",
                "WasmSharp.Benchmarks",
                "WasmSharp.Benchmarks.csproj"
            ),
            _ => Path.Combine(baseline.RepositoryDirectory, "settings", "settings.json"),
        };
        baseline.Environment = baseline.Environment with
        {
            Command = baseline.Environment.Command with
            {
                Arguments = [.. baseline.Environment.Command.Arguments, "--inspection-path=" + value],
            },
        };
        current.Environment = current.Environment with
        {
            Command = current.Environment.Command with
            {
                Arguments = [.. current.Environment.Command.Arguments, "--inspection-path="],
            },
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single();
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Condition).Contains("command.arguments[");
            await Assert.That(difference.Baseline).IsEqualTo("--inspection-path=" + expected);
            await Assert.That(difference.Current).IsEqualTo("--inspection-path=");
        }
    }

    [Test]
    public async Task 子processのruntimeが異なる_collectorが同じでも条件差を返す()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        baseline.ProcessRuntimeVersion = ".NET 10.0.13; corelib-before";
        current.ProcessRuntimeVersion = ".NET 10.0.14; corelib-after";
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == "process.runtime[0].version");
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo(baseline.ProcessRuntimeVersion);
            await Assert.That(difference.Current).IsEqualTo(current.ProcessRuntimeVersion);
        }
    }

    [Test]
    public async Task 同じJobの別caseで子processのruntimeが異なる_選択caseが一致していても比較を拒否する()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        var definition = baseline.Cases.Single();
        List<BenchmarkCase> cases = [definition, definition with { CaseId = "invoke.other" }];
        baseline.SetCases(cases);
        current.SetCases(cases);
        baseline.ProcessRuntimeVersion = ".NET 10.0.13; corelib-before";
        current.ProcessRuntimeVersion = baseline.ProcessRuntimeVersion;
        baseline.Jobs.Add(baseline.Jobs.Single() with { JobId = "secondary" });
        current.Jobs.Add(current.Jobs.Single() with { JobId = "secondary" });
        current.ProcessRuntimeVersions[(current.Jobs[0].JobId, "invoke.other")] = ".NET 10.0.14; corelib-after";
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var reason = actual.Reasons.Single();
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(reason.Kind).IsEqualTo(ComparisonReasonKind.ConditionMismatch);
            await Assert.That(reason.Description).Contains("同じJobの子process間でruntimeが異なります。");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task 子processのruntimeが未記録_collectorから補わず比較を拒否する(string? version)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.ProcessRuntimeVersion = version;
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(actual.Reasons.Single().Description).Contains("子processのruntime観測がありません");
        }
    }

    [Test]
    public async Task 予定launch数に子processが足りない_存在する観測が正常でも比較を受け入れない()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        baseline.Jobs =
        [
            baseline.Jobs.Single() with
            {
                LaunchCount = 2,
            },
            baseline.Jobs.Single() with
            {
                JobId = "secondary",
                LaunchCount = 1,
            },
        ];
        current.Jobs =
        [
            current.Jobs.Single() with
            {
                LaunchCount = 2,
            },
            current.Jobs.Single() with
            {
                JobId = "secondary",
                LaunchCount = 1,
            },
        ];
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.All(x => x.Description.Contains("全予定子process"))).IsTrue();
    }

    [Test]
    [Arguments(null, 16384, null)]
    [Arguments(16384, null, null)]
    [Arguments(0, 0, null)]
    [Arguments(16384, 1, null)]
    [Arguments(null, null, "未対応OS")]
    public async Task 子processの実行位置が欠落または前後不一致_比較を受け入れない(long? before, long? after, string? reason)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.AffinityBefore = before;
        current.AffinityAfter = after;
        current.AffinityUnavailableReason = reason;
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.ConditionMismatch)).IsTrue();
            var description = actual.Reasons.Single().Description;
            await Assert.That(description).Contains("invoke.single").And.Contains("PID=").And.Contains("affinity");
            if (reason is not null)
            {
                await Assert.That(description).Contains(reason);
            }
        }
    }

    [Test]
    public async Task 子processの実行位置がrun間で違う_同じJob要求値でも観測条件差を返す()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.AffinityBefore = 1;
        current.AffinityAfter = 1;
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == "process.affinity[0].mask");
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo("16384");
            await Assert.That(difference.Current).IsEqualTo("1");
        }
    }

    [Test]
    public async Task 保存したJobの実行位置が違う_同じCPU数でも比較条件差として返す()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        var job = BenchmarkConfig.Create().GetJobs().Single();
        baseline.Jobs = [benchmarkRun_.DescribeProcess("Measurement", job.WithAffinity((IntPtr)1))];
        current.Jobs = [benchmarkRun_.DescribeProcess("Measurement", job.WithAffinity((IntPtr)16384))];
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x =>
            x.Condition == "job.processes[0].job_settings.EnvironmentMode.Affinity"
        );
        var conditionNames = actual.Differences.Select(x => x.Condition);
        using (Assert.Multiple())
        {
            await Assert.That(conditionNames.SequenceEqual(conditionNames.Order(StringComparer.Ordinal))).IsTrue();
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo("1");
            await Assert.That(difference.Current).IsEqualTo("16384");
        }
    }

    [Test]
    public async Task 配置先と識別子とランタイムコードだけが違う_同条件として比較できる()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        baseline.Jobs.Add(baseline.Jobs.Single() with { JobId = "secondary-before" });
        current.Jobs.Add(current.Jobs.Single() with { JobId = "secondary-after" });
        current.Source = current.Source with
        {
            Head = "new-head",
            Runtime = current.Source.Runtime with
            {
                Revision = "new-runtime",
                Sha256 = "new-runtime-hash",
                Files =
                [
                    .. current.Source.Runtime.Files.Select(x =>
                        x.RelativePath.EndsWith(".cs", StringComparison.Ordinal) ? x with { Sha256 = "changed-code" } : x
                    ),
                ],
            },
            Harness = current.Source.Harness with { Revision = "new-head" },
        };
        baseline.Save();
        current.Save();
        var before = Directory
            .GetFiles(baseline.DirectoryPath, "*", SearchOption.AllDirectories)
            .Select(File.ReadAllBytes)
            .Select(x => Convert.ToHexString(SHA256.HashData(x)))
            .ToArray();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsTrue();
            await Assert.That(actual.Differences).IsEmpty();
            await Assert.That(actual.Reasons).IsEmpty();
            await Assert
                .That(
                    Directory
                        .GetFiles(baseline.DirectoryPath, "*", SearchOption.AllDirectories)
                        .Select(File.ReadAllBytes)
                        .Select(x => Convert.ToHexString(SHA256.HashData(x)))
                )
                .IsEquivalentTo(before);
        }
    }

    [Test]
    [Arguments("asset", "case.asset_sha256")]
    [Arguments("revision", "case.case_revision")]
    [Arguments("input", "case.input_hash")]
    [Arguments("expectation", "case.expected_result.values[0].bits")]
    [Arguments("operation", "case.operation_definition.operations_per_invoke")]
    [Arguments("restoration", "case.operation_definition.includes_state_restoration")]
    [Arguments("workload", "case.workload[0].count")]
    [Arguments("stage", "case.measurement_stage")]
    [Arguments("state", "case.state_policy")]
    [Arguments("harness", "source.files[tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj].sha256")]
    [Arguments("harness-code", "source.files[tools/WasmSharp.Benchmarks/Measurements/InvokeBenchmarks.cs].sha256")]
    [Arguments("runtime-project", "source.files[src/WasmSharp/WasmSharp.csproj].sha256")]
    [Arguments("shared-build", "source.files[Directory.Build.props].sha256")]
    [Arguments("dependency", "environment.benchmark_dot_net_version")]
    [Arguments("sdk", "environment.sdk_version")]
    [Arguments("runtime", "environment.runtime_version")]
    [Arguments("cpu", "environment.cpu")]
    [Arguments("os", "environment.operating_system")]
    [Arguments("architecture", "environment.process_architecture")]
    [Arguments("build", "environment.build_configuration")]
    [Arguments("gc", "environment.gc.ServerGC")]
    [Arguments("jit", "environment.jit.TieredCompilation")]
    [Arguments("env", "environment.variables.DOTNET_EnableHWIntrinsic")]
    [Arguments("env-added", "environment.variables.DOTNET_JitMinOpts")]
    [Arguments("power", "environment.power.power_source")]
    [Arguments("job", "job.processes[0].job_settings.Strategy")]
    [Arguments("runtimeconfig", "environment.processes[0].runtime_configuration.System.GC.Server")]
    [Arguments("launches", "environment.processes[0].launch_count")]
    [Arguments("argument", "command.arguments[3]")]
    [Arguments("project-path", "command.arguments[2]")]
    [Arguments("profile", "assets.profile.sha256")]
    [Arguments("tool", "asset.generation[0].tool.version")]
    [Arguments("tool-hash", "asset.generation[0].tool.sha256")]
    [Arguments("generation", "asset.generation[0].arguments[0]")]
    [Arguments("run-schema", "run.schema_version")]
    [Arguments("assets-schema", "assets.schema_version")]
    public async Task 比較条件の内容が違う_具体的な条件名と両値とrunを返す(string scenario, string condition)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        Change(current, scenario);
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == condition);
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            if (scenario == "env-added")
            {
                await Assert.That(difference.Baseline).IsNull();
            }
            else
            {
                await Assert.That(difference.Baseline).IsNotNull();
            }
            await Assert.That(difference.Current).IsNotNull();
            await Assert.That(difference.Baseline).IsNotEqualTo(difference.Current);
            await Assert.That(difference.BaselineRunId).IsEqualTo("before");
            await Assert.That(difference.CurrentRunId).IsEqualTo("after");
            await Assert.That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.ConditionMismatch)).IsTrue();
        }
    }

    [Test]
    [Arguments("planned-case", "run.planned_cases[0]", "invoke.single", "invoke.empty")]
    [Arguments("binary-path", "asset.binary.path", "binary:wasm/invoke.wasm", "binary:wasm/other.wasm")]
    [Arguments("binary-hash", "asset.binary.sha256", null, "different-binary")]
    [Arguments("source-path", "asset.sources[0].relative_path", "sources/invoke.wat", "sources/other.wat")]
    [Arguments("source-hash", "asset.sources[0].sha256", "wat-hash", "different-source")]
    [Arguments("submodule", "source.submodules[0].revision", "before-revision", "after-revision")]
    public async Task 素材や使用sourceや予定caseの条件が違う_保存先の差と区別して具体値を返す(
        string scenario,
        string condition,
        string? expectedBaseline,
        string expectedCurrent
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        if (scenario == "planned-case")
        {
            var additional = JsonSerializer
                .Deserialize<List<BenchmarkCase>>(
                    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")),
                    json_.Options
                )!
                .Single(x => x.CaseId == "invoke.empty");
            current.SetCases([current.Cases.Single(), additional]);
        }
        else if (scenario == "submodule")
        {
            baseline.Source = baseline.Source with { Submodules = [new("test/spec", "before-revision", false)] };
            current.Source = current.Source with { Submodules = [new("test/spec", "after-revision", false)] };
        }
        else
        {
            var asset = current.Assets.Assets.Single();
            var source = asset.Sources.Single();
            expectedBaseline ??= baseline.Assets.Assets.Single().Binary.Sha256;
            current.Assets = current.Assets with
            {
                Assets =
                [
                    scenario switch
                    {
                        "binary-path" => asset with { Binary = asset.Binary with { RelativePath = "wasm/other.wasm" } },
                        "binary-hash" => asset with { Binary = asset.Binary with { Sha256 = expectedCurrent } },
                        "source-path" => asset with { Sources = [source with { RelativePath = expectedCurrent }] },
                        _ => asset with { Sources = [source with { Sha256 = expectedCurrent }] },
                    },
                ],
            };
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == condition);
        using (Assert.Multiple())
        {
            await Assert.That(RunStore.Load(current.DirectoryPath, json_).Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo(expectedBaseline);
            await Assert.That(difference.Current).IsEqualTo(expectedCurrent);
            await Assert.That(difference.BaselineRunId).IsEqualTo("before");
            await Assert.That(difference.CurrentRunId).IsEqualTo("after");
            await Assert.That(actual.Reasons.Single().Kind).IsEqualTo(ComparisonReasonKind.ConditionMismatch);
        }
    }

    [Test]
    [Arguments("--artifacts", false)]
    [Arguments("--run-id", true)]
    [Arguments("--run-config", false)]
    public async Task 識別や保存だけに使うコマンド引数が違う_測定条件の差に含めない(string option, bool inline)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        foreach (var fixture in new[] { baseline, current })
        {
            var value =
                option == "--run-id"
                    ? fixture.Manifest.RunId
                    : Path.Combine(fixture.RepositoryDirectory, fixture.Manifest.RunId, "output");
            fixture.Environment = fixture.Environment with
            {
                Command = fixture.Environment.Command with
                {
                    Arguments =
                    [
                        .. fixture.Environment.Command.Arguments,
                        .. (inline ? new[] { option + "=" + value } : [option, value]),
                        "--case",
                        "invoke.single",
                    ],
                },
            };
            fixture.Save();
        }

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsTrue();
            await Assert.That(actual.Differences).IsEmpty();
            await Assert.That(actual.Reasons).IsEmpty();
        }
    }

    [Test]
    [Arguments(ResultStatus.Failed)]
    [Arguments(ResultStatus.Unavailable)]
    [Arguments(ResultStatus.Incomplete)]
    public async Task Runまたはcaseが成功していない_正常性能runと比較できない(ResultStatus status)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Manifest = current.Manifest with { Status = status };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert
            .That(actual.Reasons.Single().Description)
            .Contains(status.ToString())
            .And.Contains("case 'invoke.single'")
            .And.Contains("status=Succeeded");
    }

    [Test]
    [Arguments(RunKind.Smoke)]
    [Arguments(RunKind.Diagnostic)]
    [Arguments(RunKind.Retention)]
    public async Task 通常測定以外の用途である_正常性能runと比較できない(RunKind kind)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Manifest = current.Manifest with { Kind = kind };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains(kind.ToString());
    }

    [Test]
    [Arguments(ContentKind.Source, true, "hash")]
    [Arguments(ContentKind.Project, true, "hash")]
    [Arguments(ContentKind.Assembly, true, "hash")]
    [Arguments(ContentKind.Assembly, false, "hash")]
    [Arguments(ContentKind.Assembly, false, "path")]
    [Arguments(ContentKind.Assembly, false, "role")]
    public async Task 使用物の途中変更がある_変更一覧またはassemblyの前後差から比較を拒否する(
        ContentKind kind,
        bool listed,
        string assemblyChange
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Source = current.Source with
        {
            Changes = listed ? [new(kind, "changed", "before", "after")] : [],
            AssembliesAfter = listed
                ? current.Source.AssembliesAfter
                :
                [
                    .. current.Source.AssembliesAfter.Select(x =>
                        x.Role != AssemblyRole.Runtime
                            ? x
                            : assemblyChange switch
                            {
                                "path" => x with { Path = x.Path + ".moved" },
                                "role" => x with { Role = AssemblyRole.Harness },
                                _ => x with { Sha256 = "changed-runtime" },
                            }
                    ),
                ],
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains("途中変更");
    }

    [Test]
    public async Task 保存後に参照内容が改変される_RunStoreが検出した未完了を採用しない()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        baseline.Save();
        current.Save();
        File.WriteAllText(Path.Combine(current.DirectoryPath, "cases.json"), "[]");

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains("Incomplete");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 同じprojectを相対pathまたは別build先の実行ファイルで指定する_役割と内容で比較できる(bool executable)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        if (executable)
        {
            baseline.Environment = baseline.Environment with
            {
                Command = baseline.Environment.Command with
                {
                    ExecutablePath = Path.Combine(baseline.RepositoryDirectory, "bin/WasmSharp.Benchmarks.exe"),
                    Arguments = ["--case", "invoke.single"],
                },
            };
            current.Environment = current.Environment with
            {
                Command = current.Environment.Command with
                {
                    ExecutablePath = Path.Combine(current.RepositoryDirectory, "elsewhere/WasmSharp.Benchmarks.exe"),
                    Arguments = ["--case", "invoke.single"],
                },
            };
        }
        else
        {
            current.Environment = current.Environment with
            {
                Command = current.Environment.Command with
                {
                    Arguments =
                    [
                        .. current.Environment.Command.Arguments.Select(x =>
                            x.EndsWith(".csproj", StringComparison.Ordinal)
                                ? "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj"
                                : x
                        ),
                    ],
                },
            };
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsTrue();
        await Assert.That(actual.Differences).IsEmpty();
    }

    [Test]
    [Arguments("WasmSharp.Benchmarks.csproj", true, null, false)]
    [Arguments("Other.csproj", false, null, false)]
    [Arguments("WasmSharp.Benchmarks.csproj", true, "tools/WasmSharp.Benchmarks/Measurements/InvokeBenchmarks.cs", true)]
    [Arguments("WasmSharp.Benchmarks.csproj", true, "othertools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj", true)]
    [Arguments("WasmSharp.Benchmarks.csproj", true, "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj", false)]
    public async Task Project配下のcwdから相対指定する_未知引数があっても同じprojectだけを同条件として扱う(
        string project,
        bool comparable,
        string? externalRelativePath,
        bool beforeProject
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "before");
        using var current = new RunComparisonFixture("after", "after");
        current.Environment = current.Environment with
        {
            Command = current.Environment.Command with
            {
                WorkingDirectory = Path.Combine(current.RepositoryDirectory, "tools/WasmSharp.Benchmarks"),
                Arguments =
                [
                    .. current.Environment.Command.Arguments.Select(x =>
                        x.EndsWith(".csproj", StringComparison.Ordinal) ? project : x
                    ),
                ],
            },
        };
        if (externalRelativePath is not null)
        {
            var externalPath = Path.Combine(Path.GetTempPath(), "wasm-other-build", externalRelativePath);
            foreach (var fixture in new[] { baseline, current })
            {
                var arguments = fixture.Environment.Command.Arguments.ToList();
                arguments.Insert(beforeProject ? 1 : 3, "-p:ExternalFile=" + externalPath);
                fixture.Environment = fixture.Environment with
                {
                    Command = fixture.Environment.Command with { Arguments = arguments },
                };
            }
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = comparison_.CompareConditions(baseline.DirectoryPath, current.DirectoryPath, "invoke.single");

        // Assert
        await Assert.That(actual.IsComparable).IsEqualTo(comparable);
        if (comparable)
        {
            await Assert.That(actual.Differences).IsEmpty();
        }
        else
        {
            await Assert.That(actual.Differences.Single().Condition).IsEqualTo("command.arguments[2]");
        }
    }

    private static void Change(RunComparisonFixture fixture, string scenario)
    {
        var definition = fixture.Cases.Single();
        var environment = fixture.Environment;
        switch (scenario)
        {
            case "run-schema":
                fixture.Manifest = fixture.Manifest with { SchemaVersion = 2 };
                break;
            case "assets-schema":
                fixture.Assets = fixture.Assets with { SchemaVersion = 2 };
                break;
            case "asset":
                fixture.Cases = [definition with { AssetSha256 = "different-binary" }];
                break;
            case "revision":
                fixture.Cases = [definition with { CaseRevision = definition.CaseRevision + 1 }];
                break;
            case "input":
                fixture.Cases = [definition with { InputHash = "different-input" }];
                break;
            case "expectation":
                fixture.Cases =
                [
                    definition with
                    {
                        ExpectedResult = definition.ExpectedResult with
                        {
                            Values = [definition.ExpectedResult.Values.Single() with { Bits = "00000002" }],
                        },
                    },
                ];
                break;
            case "operation":
                fixture.Cases =
                [
                    definition with
                    {
                        OperationDefinition = definition.OperationDefinition with { OperationsPerInvoke = 2 },
                    },
                ];
                break;
            case "restoration":
                fixture.Cases =
                [
                    definition with
                    {
                        OperationDefinition = definition.OperationDefinition with { IncludesStateRestoration = true },
                    },
                ];
                break;
            case "workload":
                fixture.Cases = [definition with { Workload = [definition.Workload.Single() with { Count = 2 }] }];
                break;
            case "stage":
                fixture.Cases = [definition with { MeasurementStage = MeasurementStage.Decode }];
                break;
            case "state":
                fixture.Cases = [definition with { StatePolicy = StatePolicy.FreshModule }];
                break;
            case "harness":
            case "harness-code":
                var harnessPath =
                    scenario == "harness"
                        ? "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj"
                        : "tools/WasmSharp.Benchmarks/Measurements/InvokeBenchmarks.cs";
                fixture.Source = fixture.Source with
                {
                    Harness = fixture.Source.Harness with
                    {
                        Files =
                        [
                            .. fixture.Source.Harness.Files.Select(x =>
                                x.RelativePath == harnessPath ? x with { Sha256 = "different-harness" } : x
                            ),
                        ],
                    },
                };
                break;
            case "runtime-project":
            case "shared-build":
                var path = scenario == "runtime-project" ? "src/WasmSharp/WasmSharp.csproj" : "Directory.Build.props";
                fixture.Source = fixture.Source with
                {
                    Runtime = fixture.Source.Runtime with
                    {
                        Files =
                        [
                            .. fixture.Source.Runtime.Files.Select(x =>
                                x.RelativePath == path ? x with { Sha256 = "different-build" } : x
                            ),
                        ],
                    },
                };
                break;
            case "dependency":
                fixture.Environment = environment with { BenchmarkDotNetVersion = "0.15.8+different-full-version" };
                break;
            case "sdk":
                fixture.Environment = environment with { SdkVersion = "10.0.403" };
                break;
            case "runtime":
                fixture.Environment = environment with { RuntimeVersion = "10.0.11" };
                break;
            case "cpu":
                fixture.Environment = environment with { Cpu = "different CPU" };
                break;
            case "os":
                fixture.Environment = environment with { OperatingSystem = "different OS" };
                break;
            case "architecture":
                fixture.Environment = environment with { ProcessArchitecture = "arm64" };
                break;
            case "build":
                fixture.Environment = environment with { BuildConfiguration = "Debug" };
                break;
            case "gc":
                fixture.Environment = environment with { GcSettings = [new("ServerGC", "true")] };
                break;
            case "jit":
                fixture.Environment = environment with { JitSettings = [new("TieredCompilation", "false")] };
                break;
            case "env":
                fixture.Environment = environment with { EnvironmentVariables = [new("DOTNET_EnableHWIntrinsic", "0")] };
                break;
            case "env-added":
                fixture.Environment = environment with
                {
                    EnvironmentVariables = [.. environment.EnvironmentVariables, new("DOTNET_JitMinOpts", "1")],
                };
                break;
            case "power":
                fixture.Environment = environment with
                {
                    Power = environment.Power with { PowerSource = PowerSource.Battery },
                };
                break;
            case "job":
                fixture.Jobs = [fixture.Jobs.Single() with { JobSettings = [new("Strategy", "ColdStart")] }];
                break;
            case "runtimeconfig":
                fixture.Environment = environment with
                {
                    Processes =
                    [
                        environment.Processes.Single() with
                        {
                            RuntimeConfiguration = [new("System.GC.Server", "true")],
                        },
                    ],
                };
                break;
            case "launches":
                fixture.Environment = environment with
                {
                    Processes = [environment.Processes.Single() with { LaunchCount = 2 }],
                };
                break;
            case "argument":
                fixture.Environment = environment with
                {
                    Command = environment.Command with
                    {
                        Arguments = [.. environment.Command.Arguments.Select(x => x == "-c" ? "--verbosity" : x)],
                    },
                };
                break;
            case "project-path":
                fixture.Environment = environment with
                {
                    Command = environment.Command with
                    {
                        Arguments =
                        [
                            .. environment.Command.Arguments.Select(x =>
                                x.EndsWith(".csproj", StringComparison.Ordinal)
                                    ? Path.Combine(fixture.RepositoryDirectory, "alternate/Benchmarks.csproj")
                                    : x
                            ),
                        ],
                    },
                };
                break;
            case "profile":
                fixture.Assets = fixture.Assets with
                {
                    Profile = fixture.Assets.Profile with { Sha256 = "different-profile" },
                };
                break;
            case "tool":
            case "tool-hash":
            case "generation":
                var asset = fixture.Assets.Assets.Single();
                var step = asset.GenerationSteps.Single();
                step = scenario switch
                {
                    "tool" => step with { Tool = step.Tool with { Version = "1.0.42+different-full-version" } },
                    "tool-hash" => step with
                    {
                        Tool = step.Tool with { Executable = step.Tool.Executable with { Sha256 = "different-executable" } },
                    },
                    _ => step with { Arguments = ["--enable-feature", .. step.Arguments] },
                };
                fixture.Assets = fixture.Assets with { Assets = [asset with { GenerationSteps = [step] }] };
                break;
        }
    }
}
