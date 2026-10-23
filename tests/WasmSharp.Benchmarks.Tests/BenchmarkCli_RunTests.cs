using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests;

internal class BenchmarkCli_RunTests
{
    [Test]
    public async Task 固定素材のcheckを行う_全ケースの公開操作と期待値の一致を表示する()
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = new BenchmarkCli().Run(
            ["check", "--assets", assets.ManifestPath],
            output,
            error
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output.ToString()).Contains("invoke.single");
            await Assert.That(error.ToString()).IsEqualTo("");
        }
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 2)]
    public async Task 素材の期待値またはhashが不一致の場合_理由を表示して成立と区別する(
        bool brokenHash,
        int expectedExit
    )
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
        var exitCode = new BenchmarkCli().Run(
            ["check", "--assets", assets.ManifestPath],
            output,
            error
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(expectedExit);
            await Assert.That(error.ToString().Length > 0).IsTrue();
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
            "run" =>
            [
                "run",
                "--assets",
                assets.ManifestPath,
                "--case",
                "all",
                "--output",
                assets.DirectoryPath,
            ],
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

        // Act
        var exitCode = new BenchmarkCli().Run(args, output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(File.ReadAllBytes(assets.ManifestPath)).IsEquivalentTo(before);
            await Assert.That(error.ToString().Length > 0).IsTrue();
        }
    }

    [Test]
    [Arguments("failed", 1)]
    [Arguments("unavailable", 1)]
    [Arguments("smoke", 1)]
    [Arguments("incomplete", 2)]
    [Arguments("nested-output", 2)]
    public async Task 比較に使えないrunを指定する_失敗と未完了を区別して性能へ採用しない(
        string scenario,
        int expectedExit
    )
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
        var directory =
            scenario == "nested-output"
                ? Path.Combine(changed.DirectoryPath, "new-comparison")
                : Path.Combine(
                    Path.GetTempPath(),
                    "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N")
                );
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            // Act
            var exitCode = new BenchmarkCli().Run(
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
                if (scenario == "nested-output")
                {
                    await Assert.That(Directory.Exists(directory)).IsFalse();
                }
                else
                {
                    var report = JsonSerializer.Deserialize<ComparisonReport>(
                        File.ReadAllBytes(Path.Combine(directory, "comparison.json")),
                        CaseJson.Options
                    )!;
                    await Assert.That(report.IsAccepted).IsFalse();
                    await Assert
                        .That(report.Cases.Single().Decision)
                        .IsEqualTo(ComparisonDecision.Indeterminate);
                    await Assert
                        .That(
                            report
                                .Cases.Single()
                                .Time.Current!.Runs.Any(x => x.RunId == changed.Manifest.RunId)
                        )
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
        var directory = Path.Combine(
            Path.GetTempPath(),
            "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N")
        );
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            // Act
            var exitCode = new BenchmarkCli().Run(
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
    public async Task 測定要求が不正な場合_測定を開始せず理由と終了値2を返す(
        string option,
        string value,
        string reason
    )
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
        var exitCode = new BenchmarkCli().Run(args, output, error);

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
        var directory = Path.Combine(
            Path.GetTempPath(),
            "wasm-benchmark-cli-" + Guid.NewGuid().ToString("N")
        );
        using var output = new StringWriter();
        using var error = new StringWriter();
        var inputs = request
            .BaselineRunDirectories.Concat(request.CurrentRunDirectories)
            .SelectMany(x => Directory.GetFiles(x, "*", SearchOption.AllDirectories))
            .ToDictionary(
                x => x,
                x => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x)))
            );

        try
        {
            // Act
            var exitCode = new BenchmarkCli().Run(
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
                CaseJson.Options
            )!;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(exitCode).IsEqualTo(0);
                await Assert.That(error.ToString()).IsEqualTo("");
                await Assert.That(report.Cases.Count).IsEqualTo(15);
                await Assert.That(report.IsAccepted).IsTrue();
                await Assert.That(report.BaselineRuns[0].RunId).IsEqualTo("before-0");
                await Assert
                    .That(report.BaselineRuns[0].Manifest.Sha256)
                    .IsEqualTo(inputs[Path.Combine(request.BaselineRunDirectories[0], "run.json")]);
                await Assert.That(report.Policy.Sha256).IsEqualTo(request.PolicySha256);
                foreach (var input in inputs)
                {
                    await Assert
                        .That(
                            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input.Key)))
                        )
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
