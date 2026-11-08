using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal static class VerificationFixture
{
    internal static BenchmarkCase ChangedMemoryInputDefinition(MeasurementStage stage, CaseJson json)
    {
        var definition = Definition(stage, json);
        return AssetLoadingFixture.WithInput(
            definition with
            {
                CaseId = stage == MeasurementStage.Invoke ? "invoke.single" : definition.CaseId,
                ExpectedResult = definition.ExpectedResult with
                {
                    Arrays =
                        stage == MeasurementStage.Invoke ? [] : definition.ExpectedResult.Arrays,
                    Globals = [],
                    Tables = [],
                },
            },
            definition.Input with
            {
                Arrays =
                [
                    new(
                        "memory",
                        0,
                        ArrayElementType.U8,
                        8,
                        ArrayInputPattern.RepeatedByte,
                        null,
                        165
                    ),
                ],
            },
            json
        );
    }

    internal static WasmModule Decode()
    {
        return WasmModule.Decode(
            File.ReadAllBytes(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "Data",
                    "Verification",
                    "state.wasm"
                )
            )
        );
    }

    internal static WasmInstance Instantiate(WasmExecutionContext context)
    {
        return Decode().Validate().Instantiate(context, CaseVerifier.CreateImports());
    }

    internal static BenchmarkCase MemoryDefinition(bool matrix, CaseJson json)
    {
        var original = AssetLoadingFixture
            .ReadCases(json)
            .Single(x => x.CaseId == (matrix ? "kernel.matrix" : "memory.copy-fill"));
        var input = matrix
            ? new CaseInput(
                "matrix",
                [],
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
                ]
            )
            : new CaseInput(
                "copy_fill",
                [],
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
                    new(
                        "memory",
                        16,
                        ArrayElementType.U8,
                        8,
                        ArrayInputPattern.RepeatedByte,
                        null,
                        165
                    ),
                    new(
                        "memory",
                        24,
                        ArrayElementType.U8,
                        8,
                        ArrayInputPattern.IndexModulo256,
                        null,
                        null
                    ),
                ]
            );
        var expected = matrix
            ? new ExpectedResult(
                [],
                [
                    new(
                        "memory",
                        16,
                        ArrayElementType.U32,
                        2,
                        "sha256",
                        "2bfb141669c5f232be891f422e5e30308ad065a3e6beb25a2450fcc6d4fb6121"
                    ),
                ],
                [],
                []
            )
            : new ExpectedResult(
                [new(NumericType.I32, "000001a4")],
                [
                    new(
                        "memory",
                        0,
                        ArrayElementType.U8,
                        8,
                        "sha256",
                        "8a851ff82ee7048ad09ec3847f1ddf44944104d2cbd17ef4e3db22c6785a0d45"
                    ),
                    new(
                        "memory",
                        16,
                        ArrayElementType.U8,
                        8,
                        "sha256",
                        "ed0b4dd158a46dc424a660c0e9aaa52558406cb7c106391897bb07fd8dc3ba99"
                    ),
                    new(
                        "memory",
                        24,
                        ArrayElementType.U8,
                        8,
                        "sha256",
                        "8a851ff82ee7048ad09ec3847f1ddf44944104d2cbd17ef4e3db22c6785a0d45"
                    ),
                ],
                [],
                []
            );
        return AssetLoadingFixture.WithInput(original with { ExpectedResult = expected }, input, json);
    }

    internal static BenchmarkCase Definition(MeasurementStage stage, CaseJson json)
    {
        var original = AssetLoadingFixture.ReadCases(json).Single(x => x.CaseId == "instantiate.large");
        return AssetLoadingFixture.WithInput(
            original with
            {
                MeasurementStage = stage,
                ExpectedResult = new(
                    [new(NumericType.I32, "00000011")],
                    [
                        new(
                            "memory",
                            0,
                            ArrayElementType.U8,
                            8,
                            "sha256",
                            "8a851ff82ee7048ad09ec3847f1ddf44944104d2cbd17ef4e3db22c6785a0d45"
                        ),
                    ],
                    [
                        new("seed", new(NumericType.I32, "00000029")),
                        new("start_result", new(NumericType.I32, "0000002a")),
                    ],
                    [new("functions", 3, [new(0, "probe"), new(1, "add_one"), new(2, null)])]
                ),
            },
            new("probe", [new(NumericType.I32, "00000011")], []),
            json
        );
    }
}
