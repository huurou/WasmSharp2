using System.Text.Json;
using TUnit.Assertions.Enums;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    [Arguments("3", "passed")]
    [Arguments("5", "failed")]
    public async Task Core3のeitherを結果位置ごとに比較する_ベクトル全体の一致と各候補を保存する(
        string secondCandidateLane0,
        string expectedOutcome
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        // fはi32.const 7とv128.const i64x2 3 4を順に返す。
        var module = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 2, 0x7F, 0x7B]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 0]),
            (10, [1, 22, 0, 0x41, 7, 0xFD, 0x0C, 3, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0x0B])
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"f","args":[]},"expected":[
              {"type":"either","values":[{"type":"i32","value":"7"},{"type":"i32","value":"8"}]},
              {"type":"either","values":[{"type":"v128","lane_type":"i64","value":["3","6"]},
                                        {"type":"v128","lane_type":"i64","value":["{{secondCandidateLane0}}","4"]}]}
            ]}
            """,
            module
        );
        var profile = _profileLoader.LoadCore3() with { Inputs = [.. fixture.Manifest.Profile.Inputs] };
        var manifest = fixture.Manifest with { Profile = ProfileSnapshot.FromProfile(profile) };
        _reportStore.Save(Complete(manifest), fixture.ManifestPath);
        var cli = new RunnerCli(_profileLoader.Load(), () => profile);
        var path = Path.Combine(fixture.Root, "run.json");

        // Act
        cli.Run(["run", "--manifest", fixture.ManifestPath, "--output", path], TextWriter.Null, TextWriter.Null);

        // Assert
        using var report = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        var assertion = report.RootElement.GetProperty("inputs")[0].GetProperty("cases")[1];
        await Assert.That(assertion.GetProperty("outcome").GetString()).IsEqualTo(expectedOutcome);
        var observed = assertion.GetProperty("actual_values");
        var expected = assertion.GetProperty("expected_values");
        var scalars = expected[0].GetProperty("values").EnumerateArray();
        var vectors = expected[1].GetProperty("values").EnumerateArray();
        using (Assert.Multiple())
        {
            await Assert.That(observed[1].GetProperty("low64").GetString()).IsEqualTo("0000000000000003");
            await Assert.That(observed[1].GetProperty("high64").GetString()).IsEqualTo("0000000000000004");
            await Assert
                .That(expected.EnumerateArray().Select(x => x.GetProperty("type").GetString()!))
                .IsEquivalentTo(["either", "either"]);
            await Assert
                .That(scalars.Select(x => x.GetProperty("value").GetString()!))
                .IsEquivalentTo(["7", "8"], CollectionOrdering.Matching);
            await Assert.That(vectors.Select(x => x.GetProperty("lane_type").GetString()!)).IsEquivalentTo(["i64", "i64"]);
            await Assert
                .That(
                    vectors.Select(x => string.Join(",", x.GetProperty("lanes").EnumerateArray().Select(y => y.GetString())))
                )
                .IsEquivalentTo(["3,6", $"{secondCandidateLane0},4"], CollectionOrdering.Matching);
        }
    }
}
