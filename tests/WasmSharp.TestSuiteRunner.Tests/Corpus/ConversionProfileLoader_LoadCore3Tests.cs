using System.Security.Cryptography;
using System.Text;
using TUnit.Assertions.Enums;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ConversionProfileLoader_LoadCore3Tests
{
    private readonly ConversionProfileLoader _profileLoader = new ConversionProfileLoader();

    [Test]
    public async Task 埋込みプロファイルを読む_固定した仕様と公式入力255件の順序と内容を取得する()
    {
        // Arrange
        const string SpecCommit = "d7aada502455b3d9176ca4cbe48be4724d511d2a";
        // 固定commitのtest/coreにあるWASTの相対パスと生バイトのSHA-256をOrdinal順に並べて求めた値
        const string ExpectedInputListSha256 = "f1c6e4a4dcfbbacdf28268c667eaa1f0ce4f0a42855dec449d0d9dd71867dc66";

        // Act
        var profile = _profileLoader.LoadCore3();
        var inputList = string.Join('\n', profile.Inputs.Select(x => $"{x.Path}:{x.Sha256}"));
        var inputListSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inputList)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Id).IsEqualTo("core3");
            await Assert
                .That(profile.Spec)
                .IsEqualTo(new SourceRevision("https://github.com/WebAssembly/spec.git", SpecCommit));
            await Assert.That(profile.Inputs.Length).IsEqualTo(255);
            await Assert.That(inputListSha256).IsEqualTo(ExpectedInputListSha256);
        }
    }

    [Test]
    public async Task 埋込みプロファイルを読む_wasm_toolsの固定リリースと変換引数を取得する()
    {
        // Arrange
        var release = new WasmToolsRelease(
            "https://github.com/bytecodealliance/wasm-tools.git",
            "7a2fa422cdf9aeeaf488a2acac8bc94a078bfa32",
            "1.263.0"
        );
        string[] arguments =
        [
            "json-from-wast",
            "<relative.wast>",
            "--allow-confusing-unicode",
            "true",
            "--wasm-dir",
            "<output-root>/modules/<relative-directory>",
            "-o",
            "<output-root>/modules/<relative.json>",
        ];

        // Act
        var profile = _profileLoader.LoadCore3();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.WasmTools).IsEqualTo(release);
            await Assert.That(profile.Wabt).IsNull();
            await Assert.That(profile.WorkingDirectory).IsEqualTo("test/core");
            await Assert.That(profile.LogicalArguments).IsEquivalentTo(arguments, CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task 埋込みプロファイルを読む_対象機能16件と対象外6件を区別し変換器の既定値を持たない()
    {
        // Arrange
        string[] enabled =
        [
            "annotations",
            "bulk-memory",
            "exceptions",
            "extended-const",
            "function-references",
            "gc",
            "memory64",
            "multi-memory",
            "multi-value",
            "mutable-globals",
            "reference-types",
            "relaxed-simd",
            "saturating-float-to-int",
            "sign-extension",
            "simd",
            "tail-call",
        ];
        string[] disabled =
        [
            "code-metadata",
            "compact-imports",
            "custom-page-sizes",
            "legacy-exceptions",
            "threads",
            "wide-arithmetic",
        ];

        // Act
        var profile = _profileLoader.LoadCore3();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Features.Where(x => x.Enabled).Select(x => x.Name)).IsEquivalentTo(enabled);
            await Assert.That(profile.Features.Where(x => !x.Enabled).Select(x => x.Name)).IsEquivalentTo(disabled);
            await Assert.That(profile.Features.Where(x => x.DefaultEnabled is not null)).IsEmpty();
        }
    }
}
