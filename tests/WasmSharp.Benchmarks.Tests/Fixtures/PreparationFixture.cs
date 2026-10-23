using System.Collections.Immutable;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal static class PreparationFixture
{
    internal static LoadedAssets CreateAssets(bool invalidStart = false)
    {
        var definitions = AssetLoadingFixture
            .ReadCases()
            .Where(x => x.MeasurementStage != MeasurementStage.Invoke)
            .Select(x =>
            {
                var expected = VerificationFixture.Definition(x.MeasurementStage);
                if (invalidStart)
                {
                    expected = expected with
                    {
                        ExpectedResult = expected.ExpectedResult with
                        {
                            Globals = [new("start_result", new(NumericType.I32, "ffffffff"))],
                        },
                    };
                }
                return AssetLoadingFixture.WithInput(
                    x with
                    {
                        ExpectedResult = expected.ExpectedResult,
                    },
                    expected.Input
                );
            })
            .ToList();
        var bytes = File.ReadAllBytes(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "Data",
                    "Verification",
                    "state.wasm"
                )
            )
            .ToImmutableArray();
        return new(
            new(1, new("profile.json", ""), new("cases.json", ""), []),
            new CaseCatalog(definitions),
            new Dictionary<string, ImmutableArray<byte>>
            {
                ["invoke"] = bytes,
                ["preparation-large"] = bytes,
            }.ToImmutableDictionary(),
            definitions.ToImmutableDictionary(x => x.CaseId, InputFactory.Create)
        );
    }
}
