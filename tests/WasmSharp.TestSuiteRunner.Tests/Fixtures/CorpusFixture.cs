using System.Security.Cryptography;
using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using Artifact = WasmSharp.TestSuiteRunner.Corpus.Artifact;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal sealed class CorpusFixture : IDisposable
{
    internal const string A = "a.wast";
    internal const string B = "simd/b.wast";
    internal const string A_JSON = """
        {"source_filename": "a.wast",
         "commands": [
          {"type": "module", "line": 1, "filename": "a.0.wasm"},
          {"type": "assert_malformed", "line": 2, "filename": "a.1.wat", "text": "unexpected token", "module_type": "text"},
          {"type": "module", "line": 3, "name": "$M", "filename": "a.2.wasm"},
          {"type": "action", "line": 4, "action": {"type": "invoke", "field": "f", "args": []}, "expected": []}]}
        """;
    internal const string B_JSON = """
        {"source_filename": "simd/b.wast", "commands": [{"type": "module", "line": 1, "filename": "b.0.wasm"}]}
        """;

    internal static byte[] FirstModule { get; } = [0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00];

    internal static byte[] SecondModule { get; } = [.. FirstModule, 0x00, 0x01, 0x00];

    internal static byte[] Text { get; } = Encoding.UTF8.GetBytes("(module\n");

    private readonly TemporaryDirectory directory_ = new();

    internal string Root => directory_.Root;

    internal string SourceRoot => directory_.Combine("source");

    internal string InputRoot => Path.Combine(SourceRoot, "test", "core");

    internal string OutputRoot { get; private set; }

    internal string ManifestPath => Path.Combine(OutputRoot, "manifest.json");

    internal CorpusManifest Manifest { get; set; }

    private CorpusFixture()
    {
        OutputRoot = directory_.Combine("output");
        WriteSource(A, "(module)\n");
        WriteSource(B, "(module)\n");
        var profile = Core2Profile.Load() with
        {
            Id = "fixture",
            Inputs = [new(A, Sha256(ReadSource(A))), new(B, Sha256(ReadSource(B)))],
        };
        Manifest = CorpusManifest.Create(profile, new ConversionProvenance());
    }

    internal static CorpusFixture Create()
    {
        var fixture = new CorpusFixture();
        fixture.WriteArtifact(A, "modules/a.json", Encoding.UTF8.GetBytes(A_JSON));
        fixture.WriteArtifact(A, "modules/a.0.wasm", FirstModule);
        fixture.WriteArtifact(A, "modules/a.1.wat", Text);
        fixture.WriteArtifact(A, "modules/a.2.wasm", SecondModule);
        fixture.WriteArtifact(B, "modules/simd/b.json", Encoding.UTF8.GetBytes(B_JSON));
        fixture.WriteArtifact(B, "modules/simd/b.0.wasm", FirstModule);
        return fixture;
    }

    internal string GetPath(string artifactPath)
    {
        return Path.Combine(OutputRoot, artifactPath);
    }

    internal void WriteSource(string inputPath, string content)
    {
        var path = Path.Combine(InputRoot, inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    internal void WriteArtifact(string inputPath, string artifactPath, byte[] content)
    {
        var path = GetPath(artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        var kind = CorpusVerifier.GetKind(artifactPath)!.Value;
        var artifact = new Artifact(artifactPath, kind, Sha256(content), inputPath)
        {
            Script =
                kind == ArtifactKind.Json
                    ? CorpusVerifier.CreateScript(
                        ScriptDocument.Parse(content, artifactPath),
                        artifactPath
                    )
                    : null,
        };
        UpdateInput(
            inputPath,
            x =>
                x with
                {
                    Status = ConversionStatus.Succeeded,
                    ExitCode = 0,
                    Artifacts = [.. x.Artifacts.Where(y => y.Path != artifactPath), artifact],
                }
        );
    }

    internal void UpdateInput(
        string inputPath,
        Func<InputConversionResult, InputConversionResult> update
    )
    {
        var index = Manifest.Inputs.FindIndex(x => x.Input.Path == inputPath);
        Manifest.Inputs[index] = update(Manifest.Inputs[index]);
    }

    internal void MoveOutput()
    {
        var destination = directory_.Combine(Path.Combine("移動 先", "output"));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(OutputRoot, destination);
        OutputRoot = destination;
    }

    internal static string Sha256(byte[] content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    public void Dispose()
    {
        directory_.Dispose();
    }

    private byte[] ReadSource(string inputPath)
    {
        return File.ReadAllBytes(Path.Combine(InputRoot, inputPath));
    }
}
