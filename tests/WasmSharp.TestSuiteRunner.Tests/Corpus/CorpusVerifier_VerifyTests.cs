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
    public async Task 素材領域の下位ディレクトリを列挙できない_照合済み入力を保ち全体の診断へ理由を返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var restricted = Directory.CreateDirectory(fixture.GetPath("modules/restricted"));
        using var restriction = new DirectoryReadRestriction(restricted.FullName);

        // Act
        var result = Verify(fixture);

        // Assert
        var diagnostic = result.Diagnostics.Single();
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Operation).IsEqualTo("verify");
            await Assert.That(diagnostic.Path).IsEqualTo("modules");
            await Assert.That(diagnostic.Message).StartsWith("素材領域を列挙できません: ");
            await Assert.That(diagnostic.ExceptionType).IsEqualTo(typeof(UnauthorizedAccessException).FullName);
            await Assert.That(result.Inputs.All(x => x.Issues.IsEmpty && x.ModuleIssues.IsEmpty)).IsTrue();
            await Assert.That(Get(result, CorpusFixture.A).Modules.Count).IsEqualTo(2);
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
        await File.WriteAllBytesAsync(fixture.GetPath("modules/a.2.wasm"), CorpusFixture.FirstModule);

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
            await Assert.That(a.ModuleIssues[0].Message).IsEqualTo("素材がありません。");
            await Assert.That(a.ModuleIssues[2].Path).IsEqualTo("modules/a.2.wasm");
            await Assert.That(a.ModuleIssues[2].Message).Contains("SHA-256");
            await Assert.That(Get(result, CorpusFixture.B).Modules.Keys.SequenceEqual([0])).IsTrue();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Watが欠落または改変されている_入力異常として残しcommandの素材別の理由にしない(bool delete)
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
        await File.WriteAllBytesAsync(fixture.GetPath("modules/extra.wasm"), CorpusFixture.FirstModule);
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
    public async Task Manifestの素材pathが素材領域の相対pathではない_ファイルを利用せず理由を残す(string path)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root, "a.0.wasm"), CorpusFixture.FirstModule);
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts = [.. x.Artifacts.Select(y => y.Path == "modules/a.0.wasm" ? y with { Path = path } : y)],
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
            await Assert.That(a.Issues.Any(x => x.Path == path && x.Message.Contains("相対path"))).IsTrue();
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
        }
    }

    [Test]
    [Arguments("../../outside.wasm", "../../outside.wasm")]
    [Arguments("/outside.wasm", "/outside.wasm")]
    [Arguments("..\\outside.wasm", "..\\outside.wasm")]
    [Arguments("b.0.wasm", CorpusFixture.B)]
    [Arguments("unrecorded.wasm", "manifestに記録されていない")]
    public async Task JSONの参照名が入力の素材を指さない_利用commandに理由を残す(string filename, string expectedReason)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root, "outside.wasm"), CorpusFixture.FirstModule);
        await File.WriteAllBytesAsync(fixture.GetPath("modules/b.0.wasm"), CorpusFixture.FirstModule);
        fixture.UpdateInput(CorpusFixture.B, x =>
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
            await Assert.That(a.ModuleIssues[0].Message).Contains(expectedReason);
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.0.wasm")).IsTrue();
        }
    }

    [Test]
    public async Task 素材の所有入力が記録と異なる_所有衝突として利用commandに理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts =
                [
                    .. x.Artifacts.Select(y => y.Path == "modules/a.0.wasm" ? y with { InputPath = CorpusFixture.B } : y),
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
        fixture.UpdateInput(CorpusFixture.B, x =>
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
            await Assert.That(a.ModuleIssues[0].Message).Contains("2件");
            await Assert.That(b.Issues.Any(x => x.Path == "modules/a.0.wasm")).IsTrue();
            await Assert.That(b.Modules.ContainsKey(0)).IsTrue();
        }
    }

    [Test]
    public async Task 素材がリンクを経由して素材root外を指す_同じ内容でも利用せず最初のリンクを示す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var outsideFile = Path.Combine(fixture.Root, "outside.wasm");
        await File.WriteAllBytesAsync(outsideFile, CorpusFixture.FirstModule);
        File.Delete(fixture.GetPath("modules/a.0.wasm"));
        File.CreateSymbolicLink(fixture.GetPath("modules/a.0.wasm"), outsideFile);
        var outsideDirectory = Path.Combine(fixture.Root, "outside-simd");
        Directory.Move(fixture.GetPath("modules/simd"), outsideDirectory);
        var outsideJson = Path.Combine(fixture.Root, "outside-b.json");
        File.Move(Path.Combine(outsideDirectory, "b.json"), outsideJson);
        File.CreateSymbolicLink(Path.Combine(outsideDirectory, "b.json"), outsideJson);
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
                .That(b.Issues.Any(x => x.Path == "modules/simd/b.json" && x.Message.Contains("リンクmodules/simdを")))
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
            await Assert.That(a.Issues[0].Message).IsEqualTo("どのcommandからも参照されていない素材です。");
            await Assert.That(a.Modules.Keys.Order().SequenceEqual([0, 2])).IsTrue();
        }
    }

    [Test]
    [Arguments("source_filename")]
    [Arguments("enumeration_complete")]
    [Arguments("command_count")]
    [Arguments("commands")]
    [Arguments("references")]
    [Arguments("missing")]
    public async Task Manifestの列挙記録の1項目が生成JSONと一致しない_documentを返さない(string field)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(CorpusFixture.A, x => x with { Artifacts =
                [
                    .. x.Artifacts.Select(y =>
                        y.Script is { } script
                            ? y with
                            {
                                Script = field switch
                                {
                                    "source_filename" => script with { SourceFilename = "other.wast" },
                                    "enumeration_complete" => script with { EnumerationComplete = false },
                                    "command_count" => script with { CommandCount = 3 },
                                    "commands" => script with { Commands = [.. script.Commands.Take(3)] },
                                    "references" => script with { References = [.. script.References.Skip(1)] },
                                    "missing" => null,
                                    _ => throw new ArgumentOutOfRangeException(nameof(field)),
                                },
                            }
                            : y
                    ),
                ] });

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert.That(a.Modules).IsEmpty();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.json")).IsTrue();
            await Assert
                .That(a.Issues.Single(x => x.Path == "modules/a.json").Message)
                .IsEqualTo("manifestに記録したcommand一覧または素材参照が生成JSONと一致しません。");
        }
    }

    [Test]
    public async Task JSONが入力と同じ相対配置にない_documentを返さず配置先の理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.Move(fixture.GetPath("modules/a.json"), fixture.GetPath("modules/other.json"));
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts =
                [
                    .. x.Artifacts.Select(y => y.Kind == ArtifactKind.Json ? y with { Path = "modules/other.json" } : y),
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
                .That(a.Issues.Any(x => x.Path == "modules/other.json" && x.Message.Contains("modules/a.json")))
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
                CorpusFixture.A_JSON.Replace("\"a.wast\"", "\"/sources/a.wast\"", StringComparison.Ordinal)
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
            Encoding.UTF8.GetBytes(CorpusFixture.A_JSON.Replace("\"a.0.wasm\"", "\"a.json\"", StringComparison.Ordinal))
        );
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts =
                [
                    .. x.Artifacts.Select(y => y.Path == "modules/a.1.wat" ? y with { Kind = ArtifactKind.Wasm } : y),
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
            await Assert.That(a.ModuleIssues[0].Message).Contains("wasm");
            await Assert.That(a.ModuleIssues[0].Message).Contains("json");
            await Assert.That(a.ModuleIssues.ContainsKey(1)).IsFalse();
            await Assert.That(a.Issues.Any(x => x.Path == "modules/a.1.wat")).IsTrue();
            await Assert.That(a.Issues.Single(x => x.Path == "modules/a.1.wat").Message).Contains("wat");
            await Assert.That(a.Modules.ContainsKey(2)).IsTrue();
        }
    }

    [Test]
    public async Task 拡張子と記録した種類が異なる素材をbinaryのcommandが参照する_binaryとして渡さず理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        await File.WriteAllBytesAsync(fixture.GetPath("modules/a.3.wat"), CorpusFixture.Text);
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts =
                [
                    .. x.Artifacts,
                    new("modules/a.3.wat", ArtifactKind.Wasm, CorpusFixture.Sha256(CorpusFixture.Text), CorpusFixture.A),
                ],
            }
        );
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(CorpusFixture.A_JSON.Replace("\"a.0.wasm\"", "\"a.3.wat\"", StringComparison.Ordinal))
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
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Artifacts = [.. x.Artifacts.Where(y => y.Kind != ArtifactKind.Json)],
            }
        );
        File.Delete(fixture.GetPath("modules/a.json"));
        File.Delete(fixture.GetPath("modules/a.0.wasm"));

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Document).IsNull();
            await Assert.That(a.Issues).IsNotEmpty();
            await Assert.That(a.Issues.All(x => x.Operation == "verify")).IsTrue();
            await Assert
                .That(a.Issues.Select(x => (x.Path, x.Message)))
                .IsEquivalentTo(
                    new (string? Path, string Message)[]
                    {
                        (CorpusFixture.A, "JSONの生成物がありません。"),
                        ("modules/a.0.wasm", "素材がありません。"),
                    }
                );
        }
    }

    [Test]
    [Arguments(ConversionStatus.RunnerError, "runner_error")]
    [Arguments(ConversionStatus.Unprocessed, "unprocessed")]
    public async Task 実行モードで変換に成功していない入力を照合する_変換状態を入力異常として残す(
        ConversionStatus status,
        string statusName
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(CorpusFixture.A, x =>
            x with
            {
                Status = status,
                ExitCode = status == ConversionStatus.RunnerError ? 1 : null,
            }
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var a = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(a.Issues.Length).IsEqualTo(1);
            await Assert.That(a.Issues[0].Path).IsEqualTo(CorpusFixture.A);
            await Assert.That(a.Issues[0].Message).Contains(statusName);
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
            await Assert.That(result.Diagnostics.Select(x => x.Path).SequenceEqual(["simd/c.wast"])).IsTrue();
        }
    }

    [Test]
    public async Task JSONの生成物が複数記録されている_件数を診断して実行対象を選ばない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(CorpusFixture.A, "modules/extra.json", Encoding.UTF8.GetBytes(CorpusFixture.A_JSON));

        // Act
        var result = Verify(fixture);

        // Assert
        var input = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(input.Document).IsNull();
            await Assert.That(input.Modules).IsEmpty();
            await Assert
                .That(input.Issues)
                .Contains(new CorpusDiagnostic("verify", "JSONの生成物が2件記録されています。", CorpusFixture.A));
        }
    }

    [Test]
    public async Task JSONの列挙は完了したが未知のルート項目がある_列挙時の診断を入力異常として残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(CorpusFixture.A_JSON.Insert(1, "\"extra\":{},"))
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var input = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(input.Document).IsNotNull();
            await Assert.That(input.Document!.EnumerationComplete).IsTrue();
            await Assert
                .That(input.Issues)
                .Contains(new CorpusDiagnostic("enumerate", "固定形式にないextraがあります。", "modules/a.json"));
        }
    }

    private static readonly int[] __expectedCommandIndices = [0, 2];

    [Test]
    public async Task 同じ素材を複数commandが参照する_両方のcommandに参照数の理由を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(CorpusFixture.A_JSON.Replace("a.2.wasm", "a.0.wasm", StringComparison.Ordinal))
        );

        // Act
        var result = Verify(fixture);

        // Assert
        var input = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(input.Modules).IsEmpty();
            await Assert.That(input.ModuleIssues.Keys).IsEquivalentTo(__expectedCommandIndices);
            foreach (var issue in input.ModuleIssues.Values)
            {
                await Assert.That(issue.Path).IsEqualTo("modules/a.0.wasm");
                await Assert.That(issue.Message).IsEqualTo("2件のcommandから参照されており、commandと1対1に対応しません。");
            }
        }
    }

    [Test]
    public async Task 素材が排他的に開かれている_利用commandに読取例外を残す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        using var stream = File.Open(fixture.GetPath("modules/a.0.wasm"), FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var result = Verify(fixture);

        // Assert
        var input = Get(result, CorpusFixture.A);
        using (Assert.Multiple())
        {
            await Assert.That(input.Modules.ContainsKey(0)).IsFalse();
            await Assert.That(input.ModuleIssues[0].Path).IsEqualTo("modules/a.0.wasm");
            await Assert.That(input.ModuleIssues[0].ExceptionType).IsEqualTo(typeof(IOException).FullName);
            await Assert.That(input.ModuleIssues[0].Message).StartsWith("素材を読み取れません: ");
        }
    }

    [Test]
    public async Task 未記録のリンクが素材領域にある_リンク先を列挙せずリンク自体を報告する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var outsideDirectory = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(outsideDirectory);
        var outsideFile = Path.Combine(outsideDirectory, "outside.wasm");
        await File.WriteAllBytesAsync(outsideFile, CorpusFixture.FirstModule);
        Directory.CreateSymbolicLink(fixture.GetPath("modules/extra-directory"), outsideDirectory);
        File.CreateSymbolicLink(fixture.GetPath("modules/extra.wasm"), outsideFile);

        // Act
        var result = Verify(fixture);

        // Assert
        await Assert
            .That(result.Diagnostics)
            .IsEquivalentTo([
                new CorpusDiagnostic("verify", "manifestに記録されていないリンクがあります。", "modules/extra-directory"),
                new CorpusDiagnostic("verify", "manifestに記録されていないリンクがあります。", "modules/extra.wasm"),
            ]);
    }

    [Test]
    public async Task 素材領域がない_素材の欠落を入力に記録し列挙失敗を重ねない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        Directory.Move(fixture.GetPath("modules"), Path.Combine(fixture.Root, "moved-materials"));

        // Act
        var result = Verify(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Inputs.All(x => x.Document is null)).IsTrue();
            await Assert.That(result.Inputs.All(x => !x.Issues.IsEmpty)).IsTrue();
        }
    }

    private static CorpusVerification Verify(CorpusFixture fixture)
    {
        return CorpusVerifier.Verify(fixture.Manifest, fixture.ManifestPath, VerificationMode.Execution, null);
    }

    private static InputVerification Get(CorpusVerification result, string inputPath)
    {
        return result.Inputs.Single(x => x.Input.Path == inputPath);
    }
}
