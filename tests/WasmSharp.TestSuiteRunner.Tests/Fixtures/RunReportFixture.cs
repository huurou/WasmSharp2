using System.Security.Cryptography;
using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class RunReportFixture
{
    internal static RunReport CreateSample()
    {
        return Create(
            Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
            Case("a.wast", 1, CaseCategory.Assertion, CaseOutcome.Passed) with
            {
                Line = 2,
            },
            Case("a.wast", 2, CaseCategory.Assertion, CaseOutcome.Failed) with
            {
                Line = 2,
                ExpectedValues = [new("i32") { Value = "1" }],
                ActualValues = [new("i32") { Bits = "00000002" }],
                LastStage = CaseStage.Invoke,
            },
            Case("a.wast", 3, null, CaseOutcome.RunnerError) with
            {
                Line = null,
                Diagnostics =
                [
                    new("read_command", "未知のcommand種別です。")
                    {
                        SourceJson = """{"type": "assert_unknown"}""",
                    },
                ],
            },
            Case("b.wast", 0, CaseCategory.Setup, CaseOutcome.RuntimeUnsupported),
            Case("b.wast", 1, CaseCategory.Action, CaseOutcome.Blocked) with
            {
                Cause = new CaseCause { Direct = [new("b.wast", 0)], Origins = [new("b.wast", 0)] },
            }
        );
    }

    internal static RunReport Create(params CaseResult[] cases)
    {
        var inputs = cases
            .GroupBy(x => x.Id.InputPath, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();
        var manifest = CreateManifest([.. inputs.Select(x => (x.Key, x.Count()))]);
        var report = RunReport.Create(manifest, new RunProvenance("fixture-run"), new(1024));
        for (var i = 0; i < inputs.Length; i++)
        {
            report.Inputs[i] = report.Inputs[i] with
            {
                Status = InputRunStatus.Processed,
                CommandCount = inputs[i].Count(),
                EnumeratedCount = inputs[i].Count(),
                Cases = [.. inputs[i].OrderBy(x => x.Id.CommandIndex)],
            };
        }

        return report with
        {
            Summary = report.Summarize(),
            Completion = new RunCompletion(true, true),
        };
    }

    internal static CorpusManifest CreateManifest(params (string Path, int CommandCount)[] inputs)
    {
        var profile = Core2Profile.Load() with
        {
            Id = "fixture",
            Inputs = [.. inputs.Select(x => new SourceInput(x.Path, Sha256(x.Path)))],
        };
        var manifest = CorpusManifest.Create(
            profile,
            new ConversionProvenance { ExecutableSha256 = new string('d', 64) }
        );
        for (var i = 0; i < inputs.Length; i++)
        {
            var (path, commandCount) = inputs[i];
            var jsonPath = "modules/" + Path.ChangeExtension(path, ".json");
            manifest.Inputs[i] = manifest.Inputs[i] with
            {
                Status = ConversionStatus.Succeeded,
                ExitCode = 0,
                Artifacts =
                [
                    new(jsonPath, ArtifactKind.Json, Sha256(jsonPath), path)
                    {
                        Script = new ScriptArtifact
                        {
                            SourceFilename = path,
                            EnumerationComplete = true,
                            CommandCount = commandCount,
                            Commands =
                            [
                                .. Enumerable
                                    .Range(0, commandCount)
                                    .Select(x => new ArtifactCommand(
                                        x,
                                        x + 1,
                                        "module",
                                        null,
                                        null
                                    )),
                            ],
                        },
                    },
                ],
            };
        }

        return manifest with
        {
            Summary = manifest.Summarize(),
            Completion = new ConversionCompletion(true, true),
        };
    }

    internal static CaseResult Case(
        string inputPath,
        int commandIndex,
        CaseCategory? category,
        CaseOutcome outcome
    )
    {
        var commandType = category switch
        {
            CaseCategory.Setup => "module",
            CaseCategory.Action => "action",
            CaseCategory.Assertion => "assert_return",
            _ => null,
        };
        return new(new(inputPath, commandIndex), commandIndex + 1, commandType, category, outcome);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
