using System.Collections.Immutable;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal static class InvokeFixture
{
    internal static LoadedAssets CreateAssets(string caseId, bool invalidResult = false)
    {
        var original = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == caseId);
        BenchmarkCase definition;
        var shortInvoke = caseId.StartsWith("invoke.", StringComparison.Ordinal);
        if (shortInvoke)
        {
            definition = original;
        }
        else if (caseId is "memory.copy-fill" or "kernel.matrix")
        {
            definition = VerificationFixture.MemoryDefinition(caseId == "kernel.matrix");
        }
        else
        {
            var expected = VerificationFixture.Definition(MeasurementStage.Invoke);
            var input = expected.Input;
            if (caseId == "loop.float-host")
            {
                input = new("half", [new(NumericType.F64, "4020000000000000")], []);
                expected = expected with
                {
                    ExpectedResult = expected.ExpectedResult with
                    {
                        Values = [new(NumericType.F64, "4010000000000000")],
                    },
                };
            }
            if (caseId.StartsWith("scan.", StringComparison.Ordinal))
            {
                input = input with
                {
                    Arrays =
                    [
                        new(
                            "memory",
                            0,
                            ArrayElementType.U8,
                            8,
                            ArrayInputPattern.IndexModulo256,
                            null,
                            null
                        ),
                    ],
                };
            }
            definition = AssetLoadingFixture.WithInput(
                original with
                {
                    ExpectedResult = expected.ExpectedResult,
                },
                input
            );
        }
        if (invalidResult)
        {
            definition = definition with
            {
                ExpectedResult = definition.ExpectedResult with
                {
                    Values = [new(NumericType.I32, "ffffffff")],
                },
            };
        }
        var bytes = File.ReadAllBytes(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "Data",
                    shortInvoke ? "Loading" : "Verification",
                    shortInvoke ? "invoke.wasm" : "state.wasm"
                )
            )
            .ToImmutableArray();
        return BuildAssets(definition, bytes);
    }

    internal static LoadedAssets CreateDriftingAssets()
    {
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.single");
        definition = AssetLoadingFixture.WithInput(
            definition with
            {
                ExpectedResult = new([new(NumericType.I32, "00000000")], [], [], []),
            },
            new("next", [], [])
        );
        // 呼び出すたびに増えるglobalの変更前の値を返す 最後の結果の保持を確認する小型module
        ImmutableArray<byte> bytes =
        [
            0,
            0x61,
            0x73,
            0x6d,
            1,
            0,
            0,
            0,
            1,
            5,
            1,
            0x60,
            0,
            1,
            0x7f,
            3,
            2,
            1,
            0,
            6,
            6,
            1,
            0x7f,
            1,
            0x41,
            0,
            0x0b,
            7,
            8,
            1,
            4,
            0x6e,
            0x65,
            0x78,
            0x74,
            0,
            0,
            10,
            13,
            1,
            11,
            0,
            0x23,
            0,
            0x23,
            0,
            0x41,
            1,
            0x6a,
            0x24,
            0,
            0x0b,
        ];
        return BuildAssets(definition, bytes);
    }

    private static LoadedAssets BuildAssets(BenchmarkCase definition, ImmutableArray<byte> bytes) =>
        new(
            new(1, new("profile.json", ""), new("cases.json", ""), []),
            new CaseCatalog([definition]),
            new Dictionary<string, ImmutableArray<byte>>
            {
                [definition.AssetId] = bytes,
            }.ToImmutableDictionary(),
            new Dictionary<string, PreparedInput>
            {
                [definition.CaseId] = InputFactory.Create(definition),
            }.ToImmutableDictionary()
        );
}
