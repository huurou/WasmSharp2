using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusVerifier_VerifyTests
{
    [Test]
    public async Task 正しい素材を実行モードで照合する_入力異常なしで照合済みのdocumentとbinaryを返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        var b = Get(result, CorpusFixture.B);
        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Inputs.Length).IsEqualTo(2);
            await Assert.That(a.Issues).IsEmpty();
            await Assert.That(a.ModuleIssues).IsEmpty();
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(a.Document!.CommandCount).IsEqualTo(4);
            await Assert.That(a.Modules.Keys.Order().SequenceEqual([0, 2])).IsTrue();
            await Assert.That(a.Modules[0].SequenceEqual(CorpusFixture.FirstModule)).IsTrue();
            await Assert.That(a.Modules[2].SequenceEqual(CorpusFixture.SecondModule)).IsTrue();
            await Assert.That(b.Issues).IsEmpty();
            await Assert.That(b.Document!.SourceFilename).IsEqualTo(CorpusFixture.B);
            await Assert.That(b.Modules.Keys.SequenceEqual([0])).IsTrue();
        }
    }

    [Test]
    public async Task 相対配置を保って移動した素材を照合する_元入力と生成時のrootなしで同じ結果を返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var originalRoot = fixture.OutputRoot;
        fixture.MoveOutput();
        Directory.Delete(fixture.SourceRoot, recursive: true);

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(Directory.Exists(originalRoot)).IsFalse();
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Inputs.All(x => x.Issues.IsEmpty)).IsTrue();
            await Assert.That(result.Inputs.All(x => x.ModuleIssues.IsEmpty)).IsTrue();
            await Assert.That(a.Document!.CommandCount).IsEqualTo(4);
            await Assert.That(a.Modules.Keys.Order().SequenceEqual([0, 2])).IsTrue();
        }
    }

    [Test]
    public async Task 利用するbinaryが欠落または改変されている_利用commandだけに素材別の理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.Delete(fixture.GetPath("modules/a.0.wasm"));
        await File.WriteAllBytesAsync(
            fixture.GetPath("modules/a.2.wasm"),
            CorpusFixture.FirstModule
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues).IsEmpty();
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(a.Modules).IsEmpty();
            await Assert.That(a.ModuleIssues.Keys.Order().SequenceEqual([0, 2])).IsTrue();
            await Assert.That(a.ModuleIssues[0].Path).IsEqualTo("modules/a.0.wasm");
            await Assert.That(a.ModuleIssues[0].Operation).IsEqualTo("verify");
            await Assert.That(a.ModuleIssues[2].Path).IsEqualTo("modules/a.2.wasm");
            await Assert.That(a.ModuleIssues[2].Message).Contains("SHA-256");
            await Assert
                .That(Get(result, CorpusFixture.B).Modules.Keys.SequenceEqual([0]))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Watが欠落または改変されている_入力異常として残しcommandの素材別の理由にしない(
        bool delete
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        if (delete)
        {
            File.Delete(fixture.GetPath("modules/a.1.wat"));
        }
        else
        {
            await File.WriteAllTextAsync(fixture.GetPath("modules/a.1.wat"), "(module)\n");
        }

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo("modules/a.1.wat");
            await Assert.That(a.ModuleIssues).IsEmpty();
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(a.Modules.Keys.Order().SequenceEqual([0, 2])).IsTrue();
        }
    }

    [Test]
    public async Task JSONが改変されている_documentを返さず実行対象にしない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.AppendAllTextAsync(fixture.GetPath("modules/a.json"), " ");

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert.That(a.Modules).IsEmpty();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.json")).IsTrue();
            await Assert.That(Get(result, CorpusFixture.B).Document).IsNotNull();
        }
    }

    [Test]
    public async Task Manifestにない素材が素材領域にある_余剰として操作全体の診断に残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(
            fixture.GetPath("modules/extra.wasm"),
            CorpusFixture.FirstModule
        );
        Directory.CreateDirectory(fixture.GetPath("modules/simd/nested"));
        await File.WriteAllTextAsync(fixture.GetPath("modules/simd/nested/extra.json"), "{}");

        // Act
        var result = Verify(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    result
                        .Diagnostics.Select(x => x.Path)
                        .SequenceEqual(["modules/extra.wasm", "modules/simd/nested/extra.json"])
                )
                .IsTrue();
            await Assert.That(result.Inputs.All(x => x.Issues.IsEmpty)).IsTrue();
        }
    }

    [Test]
    [Arguments("../a.0.wasm")]
    [Arguments("modules/../a.0.wasm")]
    [Arguments("modules//a.0.wasm")]
    [Arguments("modules\\a.0.wasm")]
    [Arguments("/modules/a.0.wasm")]
    [Arguments("C:/modules/a.0.wasm")]
    [Arguments("other/a.0.wasm")]
    public async Task Manifestの素材pathが素材領域の相対pathではない_ファイルを利用せず理由を残す(
        string path
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(
            Path.Combine(fixture.Root, "a.0.wasm"),
            CorpusFixture.FirstModule
        );
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts.Select(y =>
                            y.Path == "modules/a.0.wasm" ? y with { Path = path } : y
                        ),
                    ],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues.ContainsKey(0)).IsTrue();
            await Assert
                .That(a.Issues.Any(x => x.Path == path && x.Message.Contains("相対path")))
                .IsTrue();
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
        }
    }

    [Test]
    [Arguments("../../outside.wasm")]
    [Arguments("/outside.wasm")]
    [Arguments("..\\outside.wasm")]
    [Arguments("b.0.wasm")]
    public async Task JSONの参照名が入力の素材を指さない_利用commandに理由を残す(string filename)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(
            Path.Combine(fixture.Root, "outside.wasm"),
            CorpusFixture.FirstModule
        );
        await File.WriteAllBytesAsync(
            fixture.GetPath("modules/b.0.wasm"),
            CorpusFixture.FirstModule
        );
        fixture.UpdateInput(
            CorpusFixture.B,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts,
                        new(
                            "modules/b.0.wasm",
                            ArtifactKind.Wasm,
                            CorpusFixture.Sha256(CorpusFixture.FirstModule),
                            CorpusFixture.B
                        ),
                    ],
                }
        );
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(
                CorpusFixture.A_JSON.Replace(
                    "\"a.0.wasm\"",
                    $"\"{filename.Replace("\\", "\\\\")}\"",
                    StringComparison.Ordinal
                )
            )
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues.ContainsKey(0)).IsTrue();
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.0.wasm")).IsTrue();
        }
    }

    [Test]
    public async Task 素材の所有入力が記録と異なる_所有衝突として利用commandに理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts.Select(y =>
                            y.Path == "modules/a.0.wasm"
                                ? y with
                                {
                                    InputPath = CorpusFixture.B,
                                }
                                : y
                        ),
                    ],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues[0].Path).IsEqualTo("modules/a.0.wasm");
            await Assert.That(a.ModuleIssues[0].Message).Contains(CorpusFixture.B);
        }
    }

    [Test]
    public async Task 同じ素材が複数の入力に記録されている_両方の入力に理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var shared = fixture
            .Manifest.Inputs.Single(x => x.Input.Path == CorpusFixture.A)
            .Artifacts.Single(x => x.Path == "modules/a.0.wasm");
        fixture.UpdateInput(
            CorpusFixture.B,
            x =>
                x with
                {
                    Artifacts = [.. x.Artifacts, shared with { InputPath = CorpusFixture.B }],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        var b = Get(result, CorpusFixture.B);
        using (Assert.Multiple())
        {
            await Assert.That(a.ModuleIssues[0].Path).IsEqualTo("modules/a.0.wasm");
            await Assert.That(b.Issues.Any(x => x.Path == "modules/a.0.wasm")).IsTrue();
            await Assert.That(b.Modules.ContainsKey(0)).IsTrue();
        }
    }

    [Test]
    public async Task 素材がリンクを経由して素材root外を指す_同じ内容でも利用せず理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var outsideFile = Path.Combine(fixture.Root, "outside.wasm");
        await File.WriteAllBytesAsync(outsideFile, CorpusFixture.FirstModule);
        File.Delete(fixture.GetPath("modules/a.0.wasm"));
        File.CreateSymbolicLink(fixture.GetPath("modules/a.0.wasm"), outsideFile);
        var outsideDirectory = Path.Combine(fixture.Root, "outside-simd");
        Directory.Move(fixture.GetPath("modules/simd"), outsideDirectory);
        Directory.CreateSymbolicLink(fixture.GetPath("modules/simd"), outsideDirectory);

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        var b = Get(result, CorpusFixture.B);
        using (Assert.Multiple())
        {
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues[0].Message).Contains("リンク");
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
            await Assert.That(b.Document).IsNull();
            await Assert
                .That(
                    b.Issues.Any(x =>
                        x.Path == "modules/simd/b.json" && x.Message.Contains("リンク")
                    )
                )
                .IsTrue();
            await Assert.That(result.Diagnostics).IsEmpty();
        }
    }

    [Test]
    public async Task どのcommandからも参照されない素材が記録されている_入力異常として残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.9.wasm", CorpusFixture.FirstModule);

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo("modules/a.9.wasm");
            await Assert.That(a.Modules.Keys.Order().SequenceEqual([0, 2])).IsTrue();
        }
    }

    [Test]
    public async Task Manifestのcommand一覧が生成JSONと一致しない_documentを返さない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts.Select(y =>
                            y.Script is { } script
                                ? y with
                                {
                                    Script = script with
                                    {
                                        CommandCount = 3,
                                        Commands = [.. script.Commands.Take(3)],
                                    },
                                }
                                : y
                        ),
                    ],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert.That(a.Modules).IsEmpty();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.json")).IsTrue();
        }
    }

    [Test]
    public async Task JSONが入力と同じ相対配置にない_documentを返さず配置先の理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.Move(fixture.GetPath("modules/a.json"), fixture.GetPath("modules/other.json"));
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts.Select(y =>
                            y.Kind == ArtifactKind.Json ? y with { Path = "modules/other.json" } : y
                        ),
                    ],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert
                .That(
                    a.Issues.Any(x =>
                        x.Path == "modules/other.json" && x.Message.Contains("modules/a.json")
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task JSONのsource_filenameが入力pathと異なる_入力異常として残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(
                CorpusFixture.A_JSON.Replace(
                    "\"a.wast\"",
                    "\"/sources/a.wast\"",
                    StringComparison.Ordinal
                )
            )
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo("modules/a.json");
            await Assert.That(a.Issues[0].Message).Contains("source_filename");
            await Assert.That(a.Document).IsNotNull();
        }
    }

    [Test]
    public async Task 参照先の素材の種類がcommandと対応しない_実行処理で開く側に理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(
                CorpusFixture.A_JSON.Replace("\"a.0.wasm\"", "\"a.json\"", StringComparison.Ordinal)
            )
        );
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts.Select(y =>
                            y.Path == "modules/a.1.wat" ? y with { Kind = ArtifactKind.Wasm } : y
                        ),
                    ],
                }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues[0].Path).IsEqualTo("modules/a.json");
            await Assert.That(a.ModuleIssues.ContainsKey(1)).IsFalse();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.1.wat")).IsTrue();
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
        }
    }

    [Test]
    public async Task 拡張子と記録した種類が異なる素材をbinaryのcommandが参照する_binaryとして渡さず理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(fixture.GetPath("modules/a.3.wat"), CorpusFixture.Text);
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Artifacts =
                    [
                        .. x.Artifacts,
                        new(
                            "modules/a.3.wat",
                            ArtifactKind.Wasm,
                            CorpusFixture.Sha256(CorpusFixture.Text),
                            CorpusFixture.A
                        ),
                    ],
                }
        );
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(
                CorpusFixture.A_JSON.Replace(
                    "\"a.0.wasm\"",
                    "\"a.3.wat\"",
                    StringComparison.Ordinal
                )
            )
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(a.ModuleIssues[0].Path).IsEqualTo("modules/a.3.wat");
            await Assert.That(a.ModuleIssues[0].Message).Contains("拡張子");
        }
    }

    [Test]
    public async Task JSONの生成物がない入力を照合する_documentを返さず入力異常として残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(
            CorpusFixture.A,
            x => x with { Artifacts = [.. x.Artifacts.Where(y => y.Kind != ArtifactKind.Json)] }
        );
        File.Delete(fixture.GetPath("modules/a.json"));

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert.That(a.Issues).IsNotEmpty();
            await Assert.That(a.Issues.All(x => x.Operation == "verify")).IsTrue();
        }
    }

    [Test]
    public async Task 実行モードで変換に成功していない入力を照合する_変換状態を入力異常として残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(
            CorpusFixture.A,
            x => x with { Status = ConversionStatus.RunnerError, ExitCode = 1 }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo(CorpusFixture.A);
            await Assert.That(a.Issues[0].Message).Contains("runner_error");
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(Get(result, CorpusFixture.B).Issues).IsEmpty();
        }
    }

    [Test]
    public async Task 生成モードで一致する元入力を照合する_元入力の異常を残さない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();

        // Act
        var result = CorpusVerifier.Verify(
            fixture.Manifest,
            fixture.ManifestPath,
            VerificationMode.Generation,
            fixture.SourceRoot
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Inputs.All(x => x.Issues.IsEmpty)).IsTrue();
        }
    }

    [Test]
    public async Task 生成モードで元入力に欠落と改変と余剰がある_入力別と操作全体の理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteSource(CorpusFixture.A, "(module)\r\n");
        File.Delete(Path.Combine(fixture.InputRoot, CorpusFixture.B));
        fixture.WriteSource("simd/c.wast", "(module)\n");

        // Act
        var result = CorpusVerifier.Verify(
            fixture.Manifest,
            fixture.ManifestPath,
            VerificationMode.Generation,
            fixture.SourceRoot
        );

        // Assert
        var a = Get(result, CorpusFixture.A);
        var b = Get(result, CorpusFixture.B);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo(CorpusFixture.A);
            await Assert.That(a.Issues[0].Message).Contains("SHA-256");
            await Assert.That(a.Document).IsNotNull();
            await Assert.That(b.Issues.Length).IsEqualTo(1);
            await Assert.That(b.Issues[0].Path).IsEqualTo(CorpusFixture.B);
            await Assert
                .That(result.Diagnostics.Select(x => x.Path).SequenceEqual(["simd/c.wast"]))
                .IsTrue();
        }
    }

    private static CorpusVerification Verify(CorpusFixture fixture)
    {
        return CorpusVerifier.Verify(
            fixture.Manifest,
            fixture.ManifestPath,
            VerificationMode.Execution,
            null
        );
    }

    private static InputVerification Get(CorpusVerification result, string inputPath)
    {
        return result.Inputs.Single(x => x.Input.Path == inputPath);
    }
}
