using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class CorpusManifestFixture
{
    internal static CorpusManifest Create()
    {
        var profile = Core2Profile.Load() with
        {
            Id = "fixture",
            Inputs =
            [
                new("failed.wast", new string('a', 64)),
                new("success.wast", new string('b', 64)),
                new("unprocessed.wast", new string('c', 64)),
            ],
        };
        var manifest = CorpusManifest.Create(
            profile,
            new ConversionProvenance
            {
                ExecutableSha256 = new string('d', 64),
                ExecutablePath = "/tools/wast2json",
                CreatedAt = DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
                OperatingSystem = "fixture-os",
                Architecture = "x64",
                SpecRoot = "/sources/spec",
                WabtRoot = "/sources/wabt",
                OutputRoot = "/output",
                SpecOrigin = "https://mirror.invalid/spec",
                WabtOrigin = "https://mirror.invalid/wabt",
                SpecHead = profile.Spec.Commit,
                WabtHead = profile.Wabt.Commit,
            }
        );
        var script = new ScriptArtifact
        {
            SourceFilename = "failed.wast",
            EnumerationComplete = false,
            CommandCount = null,
            Commands =
            [
                new(0, 2, "assert_malformed", "text", "failed.0.wat"),
                new(1, null, null, null, null),
            ],
            References = [new(0, "failed.0.wat", "modules/failed.0.wat")],
        };
        manifest.Inputs[0] = manifest.Inputs[0] with
        {
            Status = ConversionStatus.RunnerError,
            ExitCode = 7,
            StandardOutput = "partial output\n",
            StandardError = "conversion failed\n",
            Arguments = ["failed.wast", "-o", "/output/modules/failed.json"],
            Artifacts =
            [
                new("modules/failed.json", ArtifactKind.Json, new string('e', 64), "failed.wast")
                {
                    Script = script,
                },
                new("modules/failed.0.wat", ArtifactKind.Wat, new string('f', 64), "failed.wast"),
            ],
            Diagnostics =
            [
                new("convert", "変換に失敗しました。", "failed.wast"),
                new(
                    "enumerate",
                    "JSON末尾が破損しています。",
                    "modules/failed.json",
                    "System.Text.Json.JsonException"
                ),
            ],
        };
        manifest.Inputs[1] = manifest.Inputs[1] with
        {
            Status = ConversionStatus.Succeeded,
            ExitCode = 0,
            Artifacts =
            [
                new("modules/success.json", ArtifactKind.Json, new string('0', 64), "success.wast")
                {
                    Script = new ScriptArtifact
                    {
                        SourceFilename = "success.wast",
                        EnumerationComplete = true,
                        CommandCount = 1,
                        Commands = [new(0, 1, "module", "binary", "success.0.wasm")],
                        References = [new(0, "success.0.wasm", "modules/success.0.wasm")],
                    },
                },
                new(
                    "modules/success.0.wasm",
                    ArtifactKind.Wasm,
                    new string('1', 64),
                    "success.wast"
                ),
            ],
        };
        return manifest with
        {
            Summary = new ConversionSummary(3, 1, 1, 1, 4),
            Completion = new ConversionCompletion(false, true),
            Diagnostics = [new("generate", "中断したため未処理の入力があります。")],
        };
    }
}
