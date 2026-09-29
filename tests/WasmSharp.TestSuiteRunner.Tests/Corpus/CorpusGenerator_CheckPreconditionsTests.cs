using System.Security.Cryptography;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusGenerator_CheckPreconditionsTests
{
    private const string OTHER_COMMIT = "0000000000000000000000000000000000000000";

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
                    Convert.ToHexStringLower(
                        SHA256.HashData(await File.ReadAllBytesAsync(SuiteWorkspace.ConverterPath))
                    )
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
            await Assert
                .That(preconditions.Provenance.SpecOrigin)
                .IsEqualTo("https://mirror.invalid/spec.git");
            await Assert
                .That(preconditions.Provenance.WabtOrigin)
                .IsEqualTo("https://mirror.invalid/spec.git");
            await Assert
                .That(request.Profile.Spec.Url)
                .IsEqualTo("https://github.com/WebAssembly/spec.git");
        }
    }

    [Test]
    public async Task Spec_rootが無関係なGitの中にある管理外のコピーである_入力の一致だけで受け付ける()
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

        var profile = workspace.CreateProfile() with
        {
            Spec = new("https://github.com/WebAssembly/spec.git", OTHER_COMMIT),
        };
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
            await Assert
                .That(preconditions.Diagnostics[0].Message)
                .Contains("HEADを取得できません");
            await Assert.That(preconditions.Diagnostics[0].Message).Contains("標準エラー");
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
        profile = spec
            ? profile with
            {
                Spec = profile.Spec with { Commit = OTHER_COMMIT },
            }
            : profile with
            {
                Wabt = profile.Wabt with { Commit = OTHER_COMMIT },
            };

        // Act
        var preconditions = CorpusGenerator.CheckPreconditions(workspace.CreateRequest(profile));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preconditions.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(preconditions.Diagnostics[0].Operation).IsEqualTo("prepare");
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
        var request = workspace.CreateRequest(workspace.CreateProfile()) with
        {
            WabtRoot = wabtRoot,
        };

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
    public async Task 入力の生バイト列が固定hashと異なる_入力pathを持つ診断として記録する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        await File.WriteAllTextAsync(
            Path.Combine(workspace.InputRoot, "success.wast"),
            "(module)\r\n"
        );

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
        var converterPath = Path.Combine(workspace.OutputRoot, "missing-wast2json");
        var request = workspace.CreateRequest(workspace.CreateProfile()) with
        {
            ConverterPath = converterPath,
        };

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
