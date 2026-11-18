using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_CaptureInputsTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();
    private readonly BenchmarkRun benchmarkRun_;

    public BenchmarkRun_CaptureInputsTests()
    {
        benchmarkRun_ = new BenchmarkRun(json_, methods_);
    }

    [Test]
    public async Task 採用素材と構成を採取する_生成済み素材とケースとJob設定を同じrunへ対応付ける()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var sourceBytes = "(module)"u8.ToArray();
        File.WriteAllBytes(Path.Combine(fixture.DirectoryPath, "invoke.wat"), sourceBytes);
        var asset = fixture.Manifest.Assets.Single();
        fixture.SaveManifest(
            fixture.Manifest with
            {
                Assets =
                [
                    asset with
                    {
                        Sources =
                        [
                            new("invoke.wat", "fixed-revision", Convert.ToHexStringLower(SHA256.HashData(sourceBytes))),
                        ],
                    },
                ],
            }
        );
        using var run = new RunStoreFixture();
        var store = RunStore.Create(run.DirectoryPath, run.Manifest, json_);
        var processes = new List<ProcessConfiguration>
        {
            new("Measurement", "job-1", 1, [new("Strategy", "Throughput")], [], []),
        };

        // Act
        benchmarkRun_.CaptureInputs(store, fixture.ManifestPath, processes);

        // Assert
        var manifest = JsonSerializer.Deserialize<RunManifest>(
            File.ReadAllBytes(Path.Combine(run.DirectoryPath, "run.json")),
            json_.Options
        )!;
        var copied = AssetManifest.Load(
            Path.Combine(run.DirectoryPath, manifest.AssetManifestFile!.RelativePath),
            json_,
            methods_
        );
        using (Assert.Multiple())
        {
            await Assert.That(copied.Manifest.Cases.Sha256).IsEqualTo(fixture.Manifest.Cases.Sha256);
            await Assert.That(copied.GetBinary("invoke").Length > 0).IsTrue();
            await Assert
                .That(File.ReadAllBytes(Path.Combine(run.DirectoryPath, "inputs", "assets", "invoke.wat")))
                .IsEquivalentTo(sourceBytes);
            await Assert.That(manifest.CasesFile!.Sha256).IsEqualTo(fixture.Manifest.Cases.Sha256);
            await Assert
                .That(
                    JsonSerializer
                        .Deserialize<List<ProcessConfiguration>>(
                            File.ReadAllBytes(Path.Combine(run.DirectoryPath, manifest.JobSettingsFile!.RelativePath)),
                            json_.Options
                        )!
                        .Single()
                        .Role
                )
                .IsEqualTo("Measurement");
        }
    }

    [Test]
    public async Task 素材sourceの内容が固定hashと異なる_原因と対象pathを示して保存前に拒否する()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var sourceBytes = "(module)"u8.ToArray();
        var sourcePath = Path.Combine(fixture.DirectoryPath, "invoke.wat");
        File.WriteAllBytes(sourcePath, sourceBytes);
        var asset = fixture.Manifest.Assets.Single();
        fixture.SaveManifest(
            fixture.Manifest with
            {
                Assets =
                [
                    asset with
                    {
                        Sources =
                        [
                            new AssetSource(
                                "invoke.wat",
                                "fixed-revision",
                                Convert.ToHexStringLower(SHA256.HashData(sourceBytes))
                            ),
                        ],
                    },
                ],
            }
        );
        File.WriteAllText(sourcePath, "(module (func))");
        using var run = new RunStoreFixture();
        var store = RunStore.Create(run.DirectoryPath, run.Manifest, json_);

        // Act & Assert
        var exception = await Assert
            .That(() => benchmarkRun_.CaptureInputs(store, fixture.ManifestPath, []))
            .ThrowsExactly<InvalidDataException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains("SHA-256");
            await Assert.That(exception.Message).Contains("invoke.wat");
            await Assert.That(File.Exists(Path.Combine(run.DirectoryPath, "inputs", "assets", "invoke.wat"))).IsFalse();
        }
    }
}
