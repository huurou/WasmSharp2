using System.Text.Json;
using TUnit.Assertions.Enums;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    [Arguments((byte)2, "3", "passed")]
    [Arguments((byte)4, "3", "failed")]
    [Arguments((byte)2, "5", "failed")]
    public async Task Core3のeitherに入れ子の候補を指定する_位置ごとに照合して候補の構造と順序を保存する(
        byte scalar,
        string secondCandidateLane0,
        string expectedOutcome
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        // fは指定したi32とv128.const i64x2 3 4を順に返す。
        var module = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 2, 0x7F, 0x7B]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 0]),
            (10, [1, 22, 0, 0x41, scalar, 0xFD, 0x0C, 3, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0x0B])
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"f","args":[]},"expected":[
              {"type":"either","values":[{"type":"i32","value":"1"},
                                        {"type":"either","values":[{"type":"i32","value":"2"},{"type":"i32","value":"3"}]}]},
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
        var observed = assertion.GetProperty("actual_values");
        var expected = assertion.GetProperty("expected_values");
        using (Assert.Multiple())
        {
            await Assert.That(assertion.GetProperty("outcome").GetString()).IsEqualTo(expectedOutcome);
            await Assert.That(observed[1].GetProperty("low64").GetString()).IsEqualTo("0000000000000003");
            await Assert.That(observed[1].GetProperty("high64").GetString()).IsEqualTo("0000000000000004");
            await Assert
                .That(expected.EnumerateArray().Select(x => x.GetProperty("type").GetString()!))
                .IsEquivalentTo(["either", "either"]);
            var scalars = expected[0].GetProperty("values");
            await Assert
                .That(scalars.EnumerateArray().Select(x => x.GetProperty("type").GetString()!))
                .IsEquivalentTo(["i32", "either"], CollectionOrdering.Matching);
            await Assert.That(scalars[0].GetProperty("value").GetString()).IsEqualTo("1");
            await Assert
                .That(
                    scalars[1]
                        .GetProperty("values")
                        .EnumerateArray()
                        .Select(x => (x.GetProperty("type").GetString()!, x.GetProperty("value").GetString()!))
                )
                .IsEquivalentTo([("i32", "2"), ("i32", "3")], CollectionOrdering.Matching);
            var vectors = expected[1].GetProperty("values").EnumerateArray();
            await Assert.That(vectors.Select(x => x.GetProperty("lane_type").GetString()!)).IsEquivalentTo(["i64", "i64"]);
            await Assert
                .That(
                    vectors.Select(x => string.Join(",", x.GetProperty("lanes").EnumerateArray().Select(y => y.GetString())))
                )
                .IsEquivalentTo(["3,6", $"{secondCandidateLane0},4"], CollectionOrdering.Matching);
        }
    }
}
