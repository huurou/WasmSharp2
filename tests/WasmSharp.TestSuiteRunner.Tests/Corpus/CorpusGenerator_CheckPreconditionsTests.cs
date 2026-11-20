using System.Security.Cryptography;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusGenerator_CheckPreconditionsTests
{
    private const string OTHER_COMMIT = "0000000000000000000000000000000000000000";

    [Test]
    [Arguments("1.263.1", "7a2fa422cdf9aeeaf488a2acac8bc94a078bfa32")]
    [Arguments("1.263.0", OTHER_COMMIT)]
    public async Task WasmToolsの版またはcommitが固定条件と異なる_観測した版を出典に残して変換を拒否する(
        string version,
        string commit
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var profile = new ConversionProfileLoader().LoadCore3();
        Directory.CreateDirectory(directory.Combine(profile.WorkingDirectory));
        profile = profile with { Inputs = [], WasmTools = profile.WasmTools! with { Version = version, Commit = commit } };
        var request = new GenerateRequest(profile, directory.Root, SuiteWorkspace.ConverterPath, directory.Combine("out"));

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        var diagnostic = preconditions.Diagnostics.Single();
        const string ReportedVersion = "wasm-tools 1.263.0 (7a2fa422c 2026-10-30)";
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Provenance.ExecutableVersion).IsEqualTo(ReportedVersion);
            await Assert.That(diagnostic.Operation).IsEqualTo("prepare");
            await Assert.That(diagnostic.Path).IsEqualTo(request.ConverterPath);
            await Assert.That(diagnostic.Message).Contains($"{version} ({commit})");
            await Assert.That(diagnostic.Message).Contains(ReportedVersion);
        }
    }

    [Test]
    public async Task WasmToolsのファイルを起動できない_hashと失敗したpathを残して版を未取得とする()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var profile = new ConversionProfileLoader().LoadCore3() with { Inputs = [] };
        Directory.CreateDirectory(directory.Combine(profile.WorkingDirectory));
        // クラスライブラリは読み取れるが、変換器として単独起動できない。
        var libraryBytes = await File.ReadAllBytesAsync(typeof(WasmModule).Assembly.Location);
        var converter = directory.Combine("wasm-tools.dll");
        await File.WriteAllBytesAsync(converter, libraryBytes);
        var request = new GenerateRequest(profile, directory.Root, converter, directory.Combine("out"));

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        var diagnostic = preconditions.Diagnostics.Single();
        using (Assert.Multiple())
        {
            await Assert
                .That(preconditions.Provenance.ExecutableSha256)
                .IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(libraryBytes)));
            await Assert.That(preconditions.Provenance.ExecutableVersion).IsNull();
            await Assert.That(diagnostic.Path).IsEqualTo(converter);
            await Assert.That(diagnostic.ExceptionType).IsEqualTo(typeof(System.ComponentModel.Win32Exception).FullName);
            await Assert.That(diagnostic.Message).Contains("wasm-toolsの版を取得できません");
        }
    }

    [Test]
    public async Task 固定commitのcheckoutと一致する入力を渡す_診断なしで版と変換器hashを出典に記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        var provenance = preconditions.Provenance;
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics).IsEmpty();
            await Assert.That(provenance.SpecHead).IsEqualTo(workspace.Commit);
            await Assert.That(provenance.ExecutableVersion).IsEqualTo("wasm-tools 1.263.0 (7a2fa422c 2026-10-30)");
            await Assert
                .That(provenance.ExecutableSha256)
                .IsEqualTo(
                    Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(SuiteWorkspace.ConverterPath)))
                );
            await Assert.That(provenance.ExecutablePath).IsEqualTo(SuiteWorkspace.ConverterPath);
            await Assert.That(provenance.SpecRoot).IsEqualTo(workspace.SourceRoot);
            await Assert.That(provenance.OutputRoot).IsEqualTo(workspace.OutputRoot);
            await Assert.That(provenance.SpecOrigin).IsNull();
            await Assert.That(provenance.OperatingSystem).IsNotNull();
            await Assert.That(provenance.Architecture).IsNotNull();
        }
    }

    [Test]
    public async Task Originが上流URLと異なるミラーである_拒否せず参考出典として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        await workspace.GitAsync("remote", "add", "origin", "https://mirror.invalid/spec.git");
        var request = workspace.CreateRequest(workspace.CreateProfile());

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics).IsEmpty();
            await Assert.That(preconditions.Provenance.SpecOrigin).IsEqualTo("https://mirror.invalid/spec.git");
            await Assert.That(request.Profile.Spec.Url).IsEqualTo("https://github.com/WebAssembly/spec.git");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Spec_rootがcheckoutではないコピーである_入力の一致だけで受け付け版とoriginを取得しない(
        bool bareRepository
    )
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var copyRoot = Path.Combine(workspace.SourceRoot, "copy");
        var copyInputRoot = Path.Combine(copyRoot, "test", "core");
        Directory.CreateDirectory(copyInputRoot);
        foreach (var file in Directory.GetFiles(workspace.InputRoot))
        {
            File.Copy(file, Path.Combine(copyInputRoot, Path.GetFileName(file)));
        }

        if (bareRepository)
        {
            await workspace.GitAsync("init", "--bare", copyRoot);
            await workspace.GitAsync("-C", copyRoot, "remote", "add", "origin", "https://unrelated.invalid/spec.git");
        }

        var profile = workspace.CreateProfile() with { Spec = new("https://github.com/WebAssembly/spec.git", OTHER_COMMIT) };
        var request = workspace.CreateRequest(profile) with { SpecRoot = copyRoot };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics).IsEmpty();
            await Assert.That(preconditions.Provenance.SpecHead).IsNull();
            await Assert.That(preconditions.Provenance.SpecOrigin).IsNull();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Spec_rootに空のGitディレクトリがあり無関係なGitの中にある_親のHEADを使わず取得失敗として記録する(
        bool trailingSeparator
    )
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var copyRoot = Path.Combine(workspace.SourceRoot, "copy");
        var copyInputRoot = Path.Combine(copyRoot, "test", "core");
        Directory.CreateDirectory(copyInputRoot);
        Directory.CreateDirectory(Path.Combine(copyRoot, ".git"));
        foreach (var file in Directory.GetFiles(workspace.InputRoot))
        {
            File.Copy(file, Path.Combine(copyInputRoot, Path.GetFileName(file)));
        }

        var request = workspace.CreateRequest(workspace.CreateProfile()) with
        {
            SpecRoot = trailingSeparator ? copyRoot + Path.DirectorySeparatorChar : copyRoot,
        };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("HEADを取得できません");
            await Assert.That(preconditions.Diagnostics[0].Message).Matches(@"標準エラー: \S");
            await Assert.That(preconditions.Provenance.SpecHead).IsNull();
        }
    }

    [Test]
    public async Task SpecのHEADが固定commitと異なる_変換前提の診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        profile = profile with { Spec = profile.Spec with { Commit = OTHER_COMMIT } };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(workspace.CreateRequest(profile));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Operation).IsEqualTo("prepare");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("spec");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(OTHER_COMMIT);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(workspace.Commit);
        }
    }

    [Test]
    [NotInParallel]
    public async Task 呼出し元が別のGit配置を環境変数に指定している_要求されたcheckoutの出典を取得する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());
        var previousGitDir = Environment.GetEnvironmentVariable("GIT_DIR");
        var previousWorkTree = Environment.GetEnvironmentVariable("GIT_WORK_TREE");
        try
        {
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(workspace.OutputRoot, "other-git"));
            Environment.SetEnvironmentVariable("GIT_WORK_TREE", Path.Combine(workspace.OutputRoot, "other-tree", "missing"));

            // Act
            var preconditions = CorpusGenerator.CheckPreconditions(request);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(preconditions.Diagnostics).IsEmpty();
                await Assert.That(preconditions.Provenance.SpecHead).IsEqualTo(workspace.Commit);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_DIR", previousGitDir);
            Environment.SetEnvironmentVariable("GIT_WORK_TREE", previousWorkTree);
        }
    }

    [Test]
    [NotInParallel]
    public async Task Gitを起動できない_終了値がないことと起動失敗の原因を記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", workspace.OutputRoot);

            // Act
            var preconditions = CorpusGenerator.CheckPreconditions(request);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(preconditions.Provenance.SpecHead).IsNull();
                await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
                await Assert
                    .That(
                        preconditions.Diagnostics.All(x =>
                            x.Operation == "prepare"
                            && x.Message.Contains("終了値: なし", StringComparison.Ordinal)
                            && x.Message.Contains("gitを起動できません", StringComparison.Ordinal)
                        )
                    )
                    .IsTrue();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    [Test]
    public async Task 入力の生バイト列が固定hashと異なる_入力pathを持つ診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        await File.WriteAllTextAsync(Path.Combine(workspace.InputRoot, "success.wast"), "(module)\r\n");

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(workspace.CreateRequest(profile));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Path).IsEqualTo("success.wast");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("SHA-256");
        }
    }

    [Test]
    public async Task 変換器の実行ファイルがない_hashを記録せず変換前提の診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var converterPath = Path.Combine(workspace.OutputRoot, "missing-wasm-tools");
        var request = workspace.CreateRequest(workspace.CreateProfile()) with { ConverterPath = converterPath };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Path).IsNull();
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(converterPath);
            await Assert.That(preconditions.Provenance.ExecutableSha256).IsNull();
        }
    }
}
