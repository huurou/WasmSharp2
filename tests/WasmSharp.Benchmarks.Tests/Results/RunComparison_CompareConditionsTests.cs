using System.Security.Cryptography;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunComparison_CompareConditionsTests
{
    [Test]
    public async Task 予定launch数に子processが足りない_存在する観測が正常でも比較を受け入れない()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        baseline.Jobs = [baseline.Jobs.Single() with { LaunchCount = 2 }];
        current.Jobs = [current.Jobs.Single() with { LaunchCount = 2 }];
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert
            .That(actual.Reasons.All(x => x.Description.Contains("全予定子process")))
            .IsTrue();
    }

    [Test]
    [Arguments(null, 16384, null)]
    [Arguments(16384, null, null)]
    [Arguments(0, 0, null)]
    [Arguments(16384, 1, null)]
    [Arguments(null, null, "未対応OS")]
    public async Task 子processの実行位置が欠落または前後不一致_比較を受け入れない(
        long? before,
        long? after,
        string? reason
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.AffinityBefore = before;
        current.AffinityAfter = after;
        current.AffinityUnavailableReason = reason;
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert
                .That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.ConditionMismatch))
                .IsTrue();
        }
    }

    [Test]
    public async Task 子processの実行位置がrun間で違う_同じJob要求値でも観測条件差を返す()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.AffinityBefore = 1;
        current.AffinityAfter = 1;
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

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
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        var job = BenchmarkConfig.Create().GetJobs().Single();
        baseline.Jobs = [BenchmarkRun.DescribeProcess("Measurement", job.WithAffinity((IntPtr)1))];
        current.Jobs =
        [
            BenchmarkRun.DescribeProcess("Measurement", job.WithAffinity((IntPtr)16384)),
        ];
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        var difference = actual.Differences.Single(x =>
            x.Condition == "job.processes[0].job_settings.EnvironmentMode.Affinity"
        );
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsEqualTo("1");
            await Assert.That(difference.Current).IsEqualTo("16384");
        }
    }

    [Test]
    public async Task 配置先と識別子とランタイムコードだけが違う_同条件として比較できる()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.Source = current.Source with
        {
            Head = "new-head",
            Runtime = current.Source.Runtime with
            {
                Revision = "new-runtime",
                Sha256 = "new-runtime-hash",
                Files = current
                    .Source.Runtime.Files.Select(x =>
                        x.RelativePath.EndsWith(".cs", StringComparison.Ordinal)
                            ? x with
                            {
                                Sha256 = "changed-code",
                            }
                            : x
                    )
                    .ToList(),
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
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

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
    [Arguments(
        "harness",
        "source.files[tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj].sha256"
    )]
    [Arguments(
        "harness-code",
        "source.files[tools/WasmSharp.Benchmarks/Measurements/InvokeBenchmarks.cs].sha256"
    )]
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
    public async Task 比較条件の内容が違う_具体的な条件名と両値とrunを返す(
        string scenario,
        string condition
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        Change(current, scenario);
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        var difference = actual.Differences.Single(x => x.Condition == condition);
        using (Assert.Multiple())
        {
            await Assert.That(actual.IsComparable).IsFalse();
            await Assert.That(difference.Baseline).IsNotNull();
            await Assert.That(difference.Current).IsNotNull();
            await Assert.That(difference.Baseline).IsNotEqualTo(difference.Current);
            await Assert.That(difference.BaselineRunId).IsEqualTo("before");
            await Assert.That(difference.CurrentRunId).IsEqualTo("after");
            await Assert
                .That(actual.Reasons.Any(x => x.Kind == ComparisonReasonKind.ConditionMismatch))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(ResultStatus.Failed)]
    [Arguments(ResultStatus.Unavailable)]
    [Arguments(ResultStatus.Incomplete)]
    public async Task runまたはcaseが成功していない_正常性能runと比較できない(ResultStatus status)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.Manifest = current.Manifest with { Status = status };
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains(status.ToString());
    }

    [Test]
    [Arguments(RunKind.Smoke)]
    [Arguments(RunKind.Diagnostic)]
    [Arguments(RunKind.Retention)]
    public async Task 通常測定以外の用途である_正常性能runと比較できない(RunKind kind)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.Manifest = current.Manifest with { Kind = kind };
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains(kind.ToString());
    }

    [Test]
    [Arguments(ContentKind.Source)]
    [Arguments(ContentKind.Project)]
    [Arguments(ContentKind.Assembly)]
    public async Task 使用物の途中変更がある_変更前後のrunとして比較しない(ContentKind kind)
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.Source = current.Source with
        {
            Changes = [new(kind, "changed", "before", "after")],
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains("途中変更");
    }

    [Test]
    public async Task 保存後に参照内容が改変される_RunStoreが検出した未完了を採用しない()
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        baseline.Save();
        current.Save();
        File.WriteAllText(Path.Combine(current.DirectoryPath, "cases.json"), "[]");

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsFalse();
        await Assert.That(actual.Reasons.Single().Description).Contains("Incomplete");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 同じprojectを相対pathまたは別build先の実行ファイルで指定する_役割と内容で比較できる(
        bool executable
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        if (executable)
        {
            baseline.Environment = baseline.Environment with
            {
                Command = baseline.Environment.Command with
                {
                    ExecutablePath = "D:/before/bin/WasmSharp.Benchmarks.exe",
                    Arguments = ["--case", "invoke.single"],
                },
            };
            current.Environment = current.Environment with
            {
                Command = current.Environment.Command with
                {
                    ExecutablePath = "E:/after/elsewhere/WasmSharp.Benchmarks.exe",
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
                    Arguments = current
                        .Environment.Command.Arguments.Select(x =>
                            x.EndsWith(".csproj", StringComparison.Ordinal)
                                ? "tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj"
                                : x
                        )
                        .ToList(),
                },
            };
        }
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsTrue();
        await Assert.That(actual.Differences).IsEmpty();
    }

    [Test]
    [Arguments("WasmSharp.Benchmarks.csproj", true)]
    [Arguments("Other.csproj", false)]
    public async Task project配下のcwdから相対指定する_同じprojectだけを同条件として扱う(
        string project,
        bool comparable
    )
    {
        // Arrange
        using var baseline = new RunComparisonFixture("before", "D:/before");
        using var current = new RunComparisonFixture("after", "E:/after");
        current.Environment = current.Environment with
        {
            Command = current.Environment.Command with
            {
                WorkingDirectory = "E:/after/tools/WasmSharp.Benchmarks",
                Arguments = current
                    .Environment.Command.Arguments.Select(x =>
                        x.EndsWith(".csproj", StringComparison.Ordinal) ? project : x
                    )
                    .ToList(),
            },
        };
        baseline.Save();
        current.Save();

        // Act
        var actual = RunComparison.CompareConditions(
            baseline.DirectoryPath,
            current.DirectoryPath,
            "invoke.single"
        );

        // Assert
        await Assert.That(actual.IsComparable).IsEqualTo(comparable);
        if (comparable)
        {
            await Assert.That(actual.Differences).IsEmpty();
        }
        else
        {
            await Assert
                .That(actual.Differences.Single().Condition)
                .IsEqualTo("command.arguments[2]");
        }
    }

    private static void Change(RunComparisonFixture fixture, string scenario)
    {
        var definition = fixture.Cases.Single();
        var environment = fixture.Environment;
        switch (scenario)
        {
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
                            Values =
                            [
                                definition.ExpectedResult.Values.Single() with
                                {
                                    Bits = "00000002",
                                },
                            ],
                        },
                    },
                ];
                break;
            case "operation":
                fixture.Cases =
                [
                    definition with
                    {
                        OperationDefinition = definition.OperationDefinition with
                        {
                            OperationsPerInvoke = 2,
                        },
                    },
                ];
                break;
            case "restoration":
                fixture.Cases =
                [
                    definition with
                    {
                        OperationDefinition = definition.OperationDefinition with
                        {
                            IncludesStateRestoration = true,
                        },
                    },
                ];
                break;
            case "workload":
                fixture.Cases =
                [
                    definition with
                    {
                        Workload = [definition.Workload.Single() with { Count = 2 }],
                    },
                ];
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
                        Files = fixture
                            .Source.Harness.Files.Select(x =>
                                x.RelativePath == harnessPath
                                    ? x with
                                    {
                                        Sha256 = "different-harness",
                                    }
                                    : x
                            )
                            .ToList(),
                    },
                };
                break;
            case "runtime-project":
            case "shared-build":
                var path =
                    scenario == "runtime-project"
                        ? "src/WasmSharp/WasmSharp.csproj"
                        : "Directory.Build.props";
                fixture.Source = fixture.Source with
                {
                    Runtime = fixture.Source.Runtime with
                    {
                        Files = fixture
                            .Source.Runtime.Files.Select(x =>
                                x.RelativePath == path ? x with { Sha256 = "different-build" } : x
                            )
                            .ToList(),
                    },
                };
                break;
            case "dependency":
                fixture.Environment = environment with
                {
                    BenchmarkDotNetVersion = "0.15.8+different-full-version",
                };
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
                fixture.Environment = environment with
                {
                    JitSettings = [new("TieredCompilation", "false")],
                };
                break;
            case "env":
                fixture.Environment = environment with
                {
                    EnvironmentVariables = [new("DOTNET_EnableHWIntrinsic", "0")],
                };
                break;
            case "power":
                fixture.Environment = environment with
                {
                    Power = environment.Power with { PowerSource = PowerSource.Battery },
                };
                break;
            case "job":
                fixture.Jobs =
                [
                    fixture.Jobs.Single() with
                    {
                        JobSettings = [new("Strategy", "ColdStart")],
                    },
                ];
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
                        Arguments = environment
                            .Command.Arguments.Select(x => x == "-c" ? "--verbosity" : x)
                            .ToList(),
                    },
                };
                break;
            case "project-path":
                fixture.Environment = environment with
                {
                    Command = environment.Command with
                    {
                        Arguments = environment
                            .Command.Arguments.Select(x =>
                                x.EndsWith(".csproj", StringComparison.Ordinal)
                                    ? "E:/after/alternate/Benchmarks.csproj"
                                    : x
                            )
                            .ToList(),
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
                    "tool" => step with
                    {
                        Tool = step.Tool with { Version = "1.0.42+different-full-version" },
                    },
                    "tool-hash" => step with
                    {
                        Tool = step.Tool with
                        {
                            Executable = step.Tool.Executable with
                            {
                                Sha256 = "different-executable",
                            },
                        },
                    },
                    _ => step with { Arguments = ["--enable-feature", .. step.Arguments] },
                };
                fixture.Assets = fixture.Assets with
                {
                    Assets = [asset with { GenerationSteps = [step] }],
                };
                break;
        }
    }
}
