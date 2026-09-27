using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class Core2Profile_LoadTests
{
    [Test]
    public async Task 埋込みprofileを読む_固定版とSIMDを含む全入力を取得する()
    {
        // Arrange
        const string SPEC_COMMIT = "05ca4182176763112561ae20153975c12bd689e4";
        const string WABT_COMMIT = "03a00a1334e6121fb0cce4fccbd6bb109b68acaa";

        // Act
        var profile = Core2Profile.Load();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Id).IsEqualTo("core2");
            await Assert
                .That(profile.Spec)
                .IsEqualTo(
                    new SourceRevision("https://github.com/WebAssembly/spec.git", SPEC_COMMIT)
                );
            await Assert
                .That(profile.Wabt)
                .IsEqualTo(
                    new SourceRevision("https://github.com/WebAssembly/wabt.git", WABT_COMMIT)
                );
            await Assert.That(profile.Inputs.Length).IsEqualTo(147);
            await Assert
                .That(
                    profile.Inputs.Count(x => x.Path.StartsWith("simd/", StringComparison.Ordinal))
                )
                .IsEqualTo(57);
            await Assert
                .That(
                    profile
                        .Inputs.Select(x => x.Path)
                        .SequenceEqual(
                            profile.Inputs.Select(x => x.Path).Order(StringComparer.Ordinal)
                        )
                )
                .IsTrue();
            await Assert
                .That(profile.Inputs.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count())
                .IsEqualTo(147);
            await Assert
                .That(
                    profile.Inputs.All(x =>
                        x.Path.EndsWith(".wast", StringComparison.Ordinal)
                        && !x.Path.Contains('\\')
                        && !x.Path.StartsWith('/')
                    )
                )
                .IsTrue();
            await Assert
                .That(
                    profile.Inputs.All(x =>
                        x.Sha256.Length == 64
                        && x.Sha256.All(y => y is >= '0' and <= '9' or >= 'a' and <= 'f')
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 埋込みprofileを読む_全21機能の既定値と実効値をCore2の条件に固定する()
    {
        // Arrange
        string[] enabled =
        [
            "mutable-globals",
            "saturating-float-to-int",
            "sign-extension",
            "simd",
            "multi-value",
            "bulk-memory",
            "reference-types",
        ];
        string[] disabled =
        [
            "exceptions",
            "threads",
            "function-references",
            "tail-call",
            "annotations",
            "code-metadata",
            "gc",
            "memory64",
            "multi-memory",
            "extended-const",
            "relaxed-simd",
            "custom-page-sizes",
            "compact-imports",
            "wide-arithmetic",
        ];

        // Act
        var profile = Core2Profile.Load();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Features.Length).IsEqualTo(21);
            await Assert
                .That(profile.Features.Where(x => x.Enabled).Select(x => x.Name))
                .IsEquivalentTo(enabled);
            await Assert
                .That(profile.Features.Where(x => !x.Enabled).Select(x => x.Name))
                .IsEquivalentTo(disabled);
            await Assert.That(profile.Features.All(x => x.DefaultEnabled == x.Enabled)).IsTrue();
            await Assert
                .That(profile.Conversion)
                .IsEqualTo(new ConversionOptions(true, true, false, false));
            await Assert.That(profile.WorkingDirectory).IsEqualTo("test/core");
            await Assert
                .That(
                    profile.LogicalArguments.SequenceEqual([
                        "<relative.wast>",
                        "-o",
                        "<output-root>/modules/<relative.json>",
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 小さなprofileを読む_同じ形式で入力と変換条件を保持する()
    {
        // Arrange
        const string JSON = """
            {
              "id": "fixture",
              "spec": { "url": "https://example.invalid/spec", "commit": "fixture-spec" },
              "wabt": { "url": "https://example.invalid/wabt", "commit": "fixture-wabt" },
              "features": [{ "name": "simd", "default_enabled": true, "enabled": true }],
              "inputs": [{ "path": "sample.wast", "sha256": "sample-hash" }],
              "working_directory": "test/core",
              "logical_arguments": ["<relative.wast>", "-o", "<output-root>/modules/<relative.json>"],
              "conversion": { "check": true, "canonical_lebs": true, "relocatable": false, "debug_names": false }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JSON));

        // Act
        var profile = Core2Profile.Load(stream);
        var changedInputs = profile.Inputs.Add(new SourceInput("other.wast", "other-hash"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Id).IsEqualTo("fixture");
            await Assert.That(profile.Inputs.Length).IsEqualTo(1);
            await Assert
                .That(profile.Inputs[0])
                .IsEqualTo(new SourceInput("sample.wast", "sample-hash"));
            await Assert.That(changedInputs.Length).IsEqualTo(2);
            await Assert
                .That(profile.Features[0])
                .IsEqualTo(new FeatureSetting("simd", true, true));
            await Assert.That(profile.Conversion.Check).IsTrue();
        }
    }
}
