using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests;

internal class BenchmarkCli_RunTests
{
    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();
    private readonly BenchmarkCli _cli = new BenchmarkCli();

    [Test]
    [Arguments(ResultStatus.Succeeded, 0, false)]
    [Arguments(ResultStatus.Failed, 1, true)]
    [Arguments(ResultStatus.Unavailable, 1, false)]
    [Arguments(ResultStatus.Incomplete, 2, true)]
    public async Task 測定結果を受け取る_用途と予定を渡して指標と失敗理由と終了値を返す(
        ResultStatus status,
        int expectedExit,
        bool allCases
    )
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        assets.SaveAssets(PreparationFixture.CreateAssets(_json, _methods));
        using var run = new RunStoreFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var caseIds = allCases
            ? assets.Definitions.Select(x => x.CaseId).Order(StringComparer.Ordinal).ToArray()
            : ["decode.small"];
        var kind = allCases ? RunKind.Smoke : RunKind.Measurement;
        var expected = run.Manifest with
        {
            Kind = kind,
            Status = status,
            Cases =
            [
                .. caseIds.Select(x =>
                    run.Manifest.Cases.Single() with
                    {
                        CaseId = x,
                        Status = status,
                        Metrics =
                            status == ResultStatus.Unavailable
                                ? run.Manifest.Cases.Single().Metrics with
                                {
                                    Time = null,
                                    Allocation = null,
                                }
                                : run.Manifest.Cases.Single().Metrics,
                        Failures =
                            status == ResultStatus.Failed
                                ?
                                [
                                    new RunFailure(
                                        x,
                                        RunStage.Postcheck,
                                        RunStage.ExecutionStarted,
                                        "期待値不一致",
                                        null,
                                        "観測が異なる",
                                        null
                                    ),
                                ]
                                : [],
                    }
                ),
            ],
        };
        List<string> arguments =
        [
            "run",
            "--assets",
            assets.ManifestPath,
            "--case",
            allCases ? "all" : "decode.small",
            "--output",
            run.DirectoryPath,
        ];
        if (allCases)
        {
            arguments.AddRange(["--kind", "smoke"]);
        }
        var processArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var entryName = Path.GetFileNameWithoutExtension(typeof(BenchmarkCli_RunTests).Assembly.Location);
        RunRequest? captured = null;
        var calls = 0;

        // Act
        var exitCode = _cli.Run([.. arguments], output, error, request =>
        {
            captured = request;
            calls++;
            return expected;
        });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(expectedExit);
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(captured!.OutputDirectory).IsEqualTo(run.DirectoryPath);
            await Assert.That(captured.AssetManifestPath).IsEqualTo(assets.ManifestPath);
            await Assert.That(captured.CaseIds.SequenceEqual(caseIds)).IsTrue();
            await Assert.That(captured.Kind).IsEqualTo(kind);
            await Assert.That(captured.Config.GetJobs().Single().Id).IsEqualTo(allCases ? "Smoke" : "Measurement");
            await Assert.That(Directory.Exists(captured.RepositoryDirectory)).IsTrue();
            await Assert.That(captured.Command.ExecutablePath).IsEqualTo(Environment.ProcessPath);
            await Assert
                .That(
                    captured
                        .Command.Arguments.Prepend(captured.Command.ExecutablePath)
                        .Take(2)
                        .Select(Path.GetFileName)
                        .Any(x => x == entryName || x == entryName + ".exe" || x == entryName + ".dll")
                )
                .IsTrue();
            await Assert
                .That(captured.Command.Arguments.TakeLast(processArguments.Length).SequenceEqual(processArguments))
                .IsTrue();
            await Assert.That(captured.Command.WorkingDirectory).IsEqualTo(Environment.CurrentDirectory);
            await Assert.That(error.ToString()).IsEmpty();
            await Assert
                .That(output.ToString())
                .Contains($"run {expected.RunId}: {status}; 用途: {kind}; 保存先: {run.DirectoryPath}");
            await Assert
                .That(output.ToString())
                .Contains(
                    status == ResultStatus.Unavailable
                        ? "ns/op=Unavailable; bytes/op=Unavailable"
                        : "ns/op=125.25; bytes/op=0.000244140625"
                );
            foreach (var caseId in caseIds)
            {
                await Assert.That(output.ToString()).Contains($"{caseId}: {status};");
            }
            if (status == ResultStatus.Failed)
            {
                await Assert.That(output.ToString()).Contains("Postcheck: 期待値不一致 観測が異なる");
            }
        }
    }

    [Test]
    public async Task 測定処理が保存例外を返す_成功表示をせず同じ原因と終了値2を返す()
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = _cli.Run(
            ["run", "--assets", assets.ManifestPath, "--case", "invoke.single", "--output", run.DirectoryPath],
            output,
            error,
            _ => throw new IOException("測定結果を保存できない")
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(output.ToString()).IsEmpty();
            await Assert.That(error.ToString()).Contains("測定結果を保存できない");
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("check")]
    [Arguments("run")]
    [Arguments("compare")]
    public async Task Helpを指定する_必要な引数なしで全操作の案内を表示する(string operation)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = operation.Length == 0 ? ["--help"] : [operation, "--help"];

        // Act
        var exitCode = _cli.Run(args, output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(error.ToString()).IsEqualTo("");
            await Assert.That(output.ToString()).Contains("check --assets <manifest>");
            await Assert.That(output.ToString()).Contains("run --assets <manifest> --case <id|all>");
            await Assert.That(output.ToString()).Contains("compare --baseline <run-directory,...>");
        }
    }

    [Test]
    [Arguments("", "check、run、compare")]
    [Arguments("unknown", "check、run、compare")]
    [Arguments("run|--unknown|value", "未知の引数です: --unknown")]
    [Arguments("check|--kind|smoke", "未知の引数です: --kind")]
    [Arguments("check|--assets", "--assetsの値が不足")]
    [Arguments("check|--assets| ", "--assetsの値が不足")]
    [Arguments("check|--assets|--next", "--assetsの値が不足")]
    [Arguments("check", "必須引数が不足しています: --assets")]
    [Arguments("check|--assets|a|--assets|b", "引数が重複しています: --assets")]
    public async Task 操作または引数が不正_不足や重複を識別できる理由と終了値2を返す(string input, string reason)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = input.Length == 0 ? [] : input.Split('|');

        // Act
        var exitCode = _cli.Run(args, output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(output.ToString()).IsEqualTo("");
            await Assert.That(error.ToString()).Contains(reason);
        }
    }

    [Test]
    [Arguments(",baseline")]
    [Arguments("baseline,")]
    [Arguments("baseline,,second")]
    public async Task 比較run一覧に空要素がある_出力を作らず理由と終了値2を返す(string baseline)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = _cli.Run(
            [
                "compare",
                "--baseline",
                baseline,
                "--current",
                "current",
                "--policy",
                "policy.json",
                "--output",
                fixture.DirectoryPath,
            ],
            output,
            error
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(output.ToString()).IsEqualTo("");
            await Assert.That(error.ToString()).Contains("run一覧は空要素を含まない");
            await Assert.That(Directory.Exists(fixture.DirectoryPath)).IsFalse();
        }
    }

    [Test]
    [NotInParallel]
    [Arguments("invoke.single")]
    [Arguments("all")]
    public async Task Gitを起動できない_単一または全caseの要求を受け付けてから理由と終了値2を返す(string caseId)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var directory = Path.Combine(assets.DirectoryPath, "new-run");
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", assets.DirectoryPath);

            // Act
            var exitCode = _cli.Run(
                ["run", "--assets", assets.ManifestPath, "--case", caseId, "--output", directory],
                output,
                error
            );

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(exitCode).IsEqualTo(2);
                await Assert.That(error.ToString()).Contains("git");
                await Assert.That(output.ToString()).IsEqualTo("");
                await Assert.That(Directory.Exists(directory)).IsFalse();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 固定素材のcheckを行う_全ケースの公開操作と期待値の一致を名前順に表示する(bool preparation)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        if (preparation)
        {
            assets.SaveAssets(PreparationFixture.CreateAssets(_json, _methods));
        }
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = _cli.Run(["check", "--assets", assets.ManifestPath], output, error);

        // Assert
        var displayedCaseIds = output
            .ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split(':')[0]);
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(0);
            await Assert
                .That(displayedCaseIds.SequenceEqual(assets.Definitions.Select(x => x.CaseId).Order(StringComparer.Ordinal)))
                .IsTrue();
            if (!preparation)
            {
                await Assert.That(output.ToString()).Contains("invoke.single: Succeeded (Invoke)");
            }
            await Assert.That(error.ToString()).IsEqualTo("");
        }
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 2)]
    public async Task 素材の期待値またはhashが不一致の場合_理由を表示して成立と区別する(bool brokenHash, int expectedExit)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        if (brokenHash)
        {
            File.AppendAllText(Path.Combine(assets.DirectoryPath, "wasm", "invoke.wasm"), "broken");
        }
        else
        {
            assets.Definitions[0] = assets.Definitions[0] with
            {
                ExpectedResult = new([new(NumericType.I32, "00000000")], [], [], []),
            };
            assets.SaveCases();
        }
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = _cli.Run(["check", "--assets", assets.ManifestPath], output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(expectedExit);
            await Assert.That(error.ToString().Length > 0).IsTrue();
            if (!brokenHash)
            {
                await Assert.That(output.ToString()).Contains("invoke.single: Failed (Invoke)");
                await Assert.That(error.ToString()).Contains("invoke.single/Invoke/results[0]");
                await Assert.That(error.ToString()).Contains("期待=I32:00000000");
            }
        }
    }

    [Test]
    [Arguments("check")]
    [Arguments("run")]
    [Arguments("compare")]
    public async Task 既存出力または不正引数で操作する_入力を変更せず終了値2を返す(string operation)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var before = File.ReadAllBytes(assets.ManifestPath);
        string[] args = operation switch
        {
            "check" => ["check", "--assets", assets.ManifestPath, "--assets", assets.ManifestPath],
            "run" => ["run", "--assets", assets.ManifestPath, "--case", "all", "--output", assets.DirectoryPath],
            _ =>
            [
                "compare",
                "--baseline",
                "missing",
                "--current",
                "missing",
                "--policy",
                "missing",
                "--output",
                assets.DirectoryPath,
            ],
        };
        var runCalled = false;

        // Act
        var exitCode = _cli.Run(args, output, error, _ =>
        {
            runCalled = true;
            throw new InvalidOperationException("出力先検証を通過してはいけない");
        });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(runCalled).IsFalse();
            await Assert.That(File.ReadAllBytes(assets.ManifestPath)).IsEquivalentTo(before);
            await Assert.That(error.ToString().Length > 0).IsTrue();
            if (operation is "run" or "compare")
            {
                await Assert.That(error.ToString()).Contains("出力先は既に存在します");
            }
        }
    }

    [Test]
    [Arguments("failed", 1)]
    [Arguments("unavailable", 1)]
    [Arguments("smoke", 1)]
    [Arguments("incomplete", 2)]
    [Arguments("nested-output", 2)]
    [Arguments("null-policy", 2)]
    public async Task 比較入力が成立しない_失敗と未完了を区別して性能へ採用しない(string scenario, int expectedExit)
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        var changed = fixture.Current[0];
        changed.Manifest = scenario switch
        {
            "failed" => changed.Manifest with { Status = ResultStatus.Failed },
            "unavailable" => changed.Manifest with { Status = ResultStatus.Unavailable },
            "smoke" => changed.Manifest with { Kind = RunKind.Smoke },
            "incomplete" => changed.Manifest with { Status = ResultStatus.Incomplete },
            _ => changed.Manifest,
        };
        var request = fixture.Save();
        if (scenario == "null-policy")
        {
            File.WriteAllText(request.PolicyPath, "null");
        }
        var directory =
            scenario == "nested-output"
                ? Path.Combine(changed.DirectoryPath, "new-comparison")
                : Path.Combine(Path.GetTempPath(), "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            // Act
            var exitCode = _cli.Run(
                [
                    "compare",
                    "--baseline",
                    string.Join(',', request.BaselineRunDirectories),
                    "--current",
                    string.Join(',', request.CurrentRunDirectories),
                    "--policy",
                    request.PolicyPath,
                    "--output",
                    directory,
                ],
                output,
                error
            );

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(exitCode).IsEqualTo(expectedExit);
                if (scenario is "nested-output" or "null-policy")
                {
                    await Assert.That(Directory.Exists(directory)).IsFalse();
                    await Assert.That(output.ToString()).IsEmpty();
                    await Assert
                        .That(error.ToString())
                        .Contains(scenario == "null-policy" ? "policyのJSONがnull" : "比較入力run内");
                }
                else
                {
                    var report = JsonSerializer.Deserialize<ComparisonReport>(
                        File.ReadAllBytes(Path.Combine(directory, "comparison.json")),
                        _json.Options
                    )!;
                    await Assert.That(report.IsAccepted).IsFalse();
                    await Assert.That(report.Cases.Single().Decision).IsEqualTo(ComparisonDecision.Indeterminate);
                    await Assert
                        .That(report.Cases.Single().Time.Current!.Runs.Any(x => x.RunId == changed.Manifest.RunId))
                        .IsFalse();
                    await Assert.That(report.Cases.Single().BlocksCompletion).IsTrue();
                }
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(true, 1)]
    public async Task CLIコマンドを保存したrunを比較する_素材配置差だけを除き意味のある引数差を保持する(
        bool changedKind,
        int expectedExit
    )
    {
        // Arrange
        using var fixture = new ComparisonFixture();
        foreach (var run in fixture.Baseline.Concat(fixture.Current))
        {
            var root = run.Environment.Command.WorkingDirectory;
            run.Environment = run.Environment with
            {
                Command = new(
                    root + "/bin/WasmSharp.Benchmarks.exe",
                    [
                        "run",
                        "--assets",
                        root
                            + (
                                fixture.Current.Contains(run)
                                    ? "/relocated-assets/asset-manifest.json"
                                    : "/new-assets/asset-manifest.json"
                            ),
                        "--case",
                        "invoke.single",
                        "--kind",
                        changedKind && fixture.Current.Contains(run) ? "smoke" : "measurement",
                        "--output",
                        root + "/saved/" + run.Manifest.RunId,
                    ],
                    root
                ),
            };
        }
        var request = fixture.Save();
        var directory = Path.Combine(Path.GetTempPath(), "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            // Act
            var exitCode = _cli.Run(
                [
                    "compare",
                    "--baseline",
                    string.Join(',', request.BaselineRunDirectories),
                    "--current",
                    string.Join(',', request.CurrentRunDirectories),
                    "--policy",
                    request.PolicyPath,
                    "--output",
                    directory,
                ],
                output,
                error
            );

            // Assert
            await Assert.That(exitCode).IsEqualTo(expectedExit);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    [Arguments("--kind", "diagnostic", "用途")]
    [Arguments("--case", "missing", "未登録")]
    public async Task 測定要求が不正な場合_測定を開始せず理由と終了値2を返す(string option, string value, string reason)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var directory = Path.Combine(assets.DirectoryPath, "new-run");
        string[] args =
        [
            "run",
            "--assets",
            assets.ManifestPath,
            "--case",
            option == "--case" ? value : "invoke.single",
            "--output",
            directory,
            "--kind",
            option == "--kind" ? value : "smoke",
        ];

        // Act
        var exitCode = _cli.Run(args, output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(error.ToString()).Contains(reason);
            await Assert.That(Directory.Exists(directory)).IsFalse();
        }
    }

    [Test]
    public async Task 保存run群を比較する_全ケースの受入判定と入力IDとhashを新規保存する()
    {
        // Arrange
        using var fixture = new ComparisonFixture(allCases: true);
        var request = fixture.Save();
        var directory = Path.Combine(Path.GetTempPath(), "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var inputs = request
            .BaselineRunDirectories.Concat(request.CurrentRunDirectories)
            .SelectMany(x => Directory.GetFiles(x, "*", SearchOption.AllDirectories))
            .ToDictionary(x => x, x => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x))));

        try
        {
            // Act
            var exitCode = _cli.Run(
                [
                    "compare",
                    "--baseline",
                    string.Join(',', request.BaselineRunDirectories),
                    "--current",
                    string.Join(',', request.CurrentRunDirectories),
                    "--policy",
                    request.PolicyPath,
                    "--output",
                    directory,
                ],
                output,
                error
            );
            var report = JsonSerializer.Deserialize<ComparisonReport>(
                File.ReadAllBytes(Path.Combine(directory, "comparison.json")),
                _json.Options
            )!;

            // Assert
            var caseIds = report.Cases.Select(x => x.CaseId);
            using (Assert.Multiple())
            {
                await Assert.That(exitCode).IsEqualTo(0);
                await Assert.That(error.ToString()).IsEqualTo("");
                await Assert.That(report.Cases.Count).IsEqualTo(15);
                await Assert.That(caseIds.SequenceEqual(caseIds.Order(StringComparer.Ordinal))).IsTrue();
                await Assert.That(report.IsAccepted).IsTrue();
                await Assert.That(output.ToString()).Contains("invoke.single: Improved");
                await Assert.That(output.ToString()).Contains($"受入: True; 保存先: {directory}");
                await Assert.That(output.ToString()).Contains("  ReductionTargetReached:");
                await Assert.That(report.BaselineRuns[0].RunId).IsEqualTo("before-0");
                await Assert
                    .That(report.BaselineRuns[0].Manifest.Sha256)
                    .IsEqualTo(inputs[Path.Combine(request.BaselineRunDirectories[0], "run.json")]);
                await Assert.That(report.Policy.Sha256).IsEqualTo(request.PolicySha256);
                foreach (var input in inputs)
                {
                    await Assert
                        .That(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input.Key))))
                        .IsEqualTo(input.Value);
                }
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
