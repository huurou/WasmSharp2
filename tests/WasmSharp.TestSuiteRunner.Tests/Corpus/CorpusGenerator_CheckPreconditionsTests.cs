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
        var request = new GenerateRequest(
            profile,
            directory.Root,
            null,
            SuiteWorkspace.ConverterPath,
            directory.Combine("out")
        );

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
        var request = new GenerateRequest(profile, directory.Root, null, converter, directory.Combine("out"));

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
            await Assert.That(provenance.WabtHead).IsEqualTo(workspace.Commit);
            await Assert
                .That(provenance.ExecutableSha256)
                .IsEqualTo(
                    Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(SuiteWorkspace.ConverterPath)))
                );
            await Assert.That(provenance.ExecutablePath).IsEqualTo(SuiteWorkspace.ConverterPath);
            await Assert.That(provenance.SpecRoot).IsEqualTo(workspace.SourceRoot);
            await Assert.That(provenance.WabtRoot).IsEqualTo(workspace.SourceRoot);
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
            await Assert.That(preconditions.Provenance.WabtOrigin).IsEqualTo("https://mirror.invalid/spec.git");
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
            await Assert.That(preconditions.Provenance.WabtHead).IsEqualTo(workspace.Commit);
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
            await Assert.That(preconditions.Provenance.WabtHead).IsEqualTo(workspace.Commit);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task HEADが固定commitと異なる_変換前提の診断として記録する(bool spec)
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        var wabt = profile.Wabt!;
        profile = spec
            ? profile with
            {
                Spec = profile.Spec with { Commit = OTHER_COMMIT },
            }
            : profile with
            {
                Wabt = wabt with { Revision = wabt.Revision with { Commit = OTHER_COMMIT } },
            };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(workspace.CreateRequest(profile));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Operation).IsEqualTo("prepare");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(spec ? "spec" : "WABT");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(OTHER_COMMIT);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(workspace.Commit);
        }
    }

    [Test]
    public async Task WABT_rootがGitのcheckoutではない_変換前提の診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var wabtRoot = Path.Combine(workspace.OutputRoot, "wabt");
        Directory.CreateDirectory(wabtRoot);
        var request = workspace.CreateRequest(workspace.CreateProfile(), wabtRoot);

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Path).IsNull();
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(wabtRoot);
            await Assert.That(preconditions.Provenance.WabtHead).IsNull();
        }
    }

    [Test]
    public async Task WABTのcheckoutにcommitがなくoriginだけがある_取得失敗を記録しoriginを出典に採用しない()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var wabtRoot = Path.Combine(workspace.OutputRoot, "unborn-wabt");
        await workspace.GitAsync("init", wabtRoot);
        await workspace.GitAsync("-C", wabtRoot, "remote", "add", "origin", "https://unrelated.invalid/wabt.git");
        var request = workspace.CreateRequest(workspace.CreateProfile(), wabtRoot);

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Provenance.WabtHead).IsNull();
            await Assert.That(preconditions.Provenance.WabtOrigin).IsNull();
            await Assert.That(preconditions.Provenance.SpecHead).IsEqualTo(workspace.Commit);
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("HEADを取得できません");
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
                await Assert.That(preconditions.Provenance.WabtHead).IsEqualTo(workspace.Commit);
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
                await Assert.That(preconditions.Provenance.WabtHead).IsNull();
                await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(2);
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
    public async Task Git情報が破損したWABT配置で前提を確認する_HEAD取得失敗の終了値と配置を診断に残す()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var wabtRoot = Path.Combine(workspace.OutputRoot, "broken-wabt");
        Directory.CreateDirectory(Path.Combine(wabtRoot, ".git"));
        var request = workspace.CreateRequest(workspace.CreateProfile(), wabtRoot);

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(request);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Provenance.WabtHead).IsNull();
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains(wabtRoot);
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("HEADを取得できません。終了値: 128");
        }
    }

    [Test]
    public async Task 変換器の実行ファイルがない_hashを記録せず変換前提の診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var converterPath = Path.Combine(workspace.OutputRoot, "missing-wast2json");
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
