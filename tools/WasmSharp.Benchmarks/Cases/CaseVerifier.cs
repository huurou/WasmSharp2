using System.Security.Cryptography;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 公開操作の観測を固定期待値と計時外で照合する。
/// </summary>
internal static class CaseVerifier
{
    internal static VerificationResult Verify(BenchmarkCase definition, CaseObservation observation)
    {
        var stage = definition.MeasurementStage.ToString();
        try
        {
            switch (definition.MeasurementStage, observation)
            {
                case (
                    MeasurementStage.Decode
                        or MeasurementStage.DecodeValidate,
                    ModuleObservation decoded
                ):
                    if (definition.MeasurementStage == MeasurementStage.Decode)
                    {
                        stage = "Validate";
                        decoded.Module.Validate();
                    }
                    stage = "Instantiate";
                    var instance = decoded.Module.Instantiate(CreateImports());
                    return VerifyInstance(definition, instance, ref stage);
                case (MeasurementStage.Instantiate, InstanceObservation instantiated):
                    return VerifyInstance(definition, instantiated.Instance, ref stage);
                case (MeasurementStage.Invoke, InvokeObservation invoked):
                    var result = VerifyResults(definition, invoked.Results);
                    return result.Succeeded
                        ? VerifyState(definition, invoked.Instance, stage) ?? result
                        : result;
                default:
                    return new(
                        false,
                        definition.CaseId,
                        stage,
                        "observation",
                        definition.MeasurementStage switch
                        {
                            MeasurementStage.Decode or MeasurementStage.DecodeValidate => nameof(
                                ModuleObservation
                            ),
                            MeasurementStage.Instantiate => nameof(InstanceObservation),
                            _ => nameof(InvokeObservation),
                        },
                        observation.GetType().Name
                    );
            }
        }
        catch (Exception exception)
        {
            return new(
                false,
                definition.CaseId,
                stage,
                "exception",
                "正常終了",
                $"{exception.GetType().FullName}: {exception.Message}"
            );
        }
    }

    internal static WasmImports CreateImports()
    {
        var module = new WasmHostModule("bench");
        module.Define(
            "half",
            WasmFunction.CreateHost(
                new([WasmValueKind.F64], [WasmValueKind.F64]),
                arguments => new([WasmValue.FromF64(arguments[0].AsF64() * 0.5)])
            )
        );
        module.Define(
            "add_one",
            WasmFunction.CreateHost(
                new([WasmValueKind.I32], [WasmValueKind.I32]),
                arguments => new([WasmValue.FromI32(unchecked(arguments[0].AsI32() + 1))])
            )
        );
        var imports = new WasmImports();
        imports.Add(module);
        return imports;
    }

    private static VerificationResult VerifyInstance(
        BenchmarkCase definition,
        WasmInstance instance,
        ref string stage
    )
    {
        // 追加Invokeによって初期化やstartの不一致が修復される前に確認する。
        var initialState = VerifyState(definition, instance, stage);
        if (initialState is not null)
        {
            return initialState;
        }
        var input = InputFactory.Create(definition);
        foreach (var array in input.Arrays)
        {
            instance.GetMemory(array.MemoryExport).Write(array.Offset, array.Bytes.AsSpan());
        }
        stage = "Invoke";
        var result = VerifyResults(
            definition,
            instance.GetFunction(input.FunctionExport).Invoke(input.Arguments.AsSpan())
        );
        return result.Succeeded ? VerifyState(definition, instance, stage) ?? result : result;
    }

    private static VerificationResult? VerifyState(
        BenchmarkCase definition,
        WasmInstance instance,
        string stage
    )
    {
        foreach (var array in definition.ExpectedResult.Arrays)
        {
            if (array.HashAlgorithm != "sha256")
            {
                return new(
                    false,
                    definition.CaseId,
                    stage,
                    "hash_algorithm",
                    "sha256",
                    array.HashAlgorithm
                );
            }
            var bytes = new byte[ByteLength(array.ElementType, array.Length)];
            instance.GetMemory(array.MemoryExport).Read(array.Offset, bytes);
            var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (digest != array.Digest)
            {
                return new(
                    false,
                    definition.CaseId,
                    stage,
                    $"memory.{array.MemoryExport}[{array.Offset}..{(ulong)array.Offset + (uint)bytes.Length}]",
                    array.Digest,
                    digest
                );
            }
        }
        // 出力digestで覆われない固定入力も確認する 行列の入力を復元して不一致を隠さない。
        if (definition.Input.Arrays.Count > 0)
        {
            foreach (var input in InputFactory.Create(definition).Arrays)
            {
                if (
                    definition.ExpectedResult.Arrays.Any(x =>
                        x.MemoryExport == input.MemoryExport
                        && x.Offset == input.Offset
                        && ByteLength(x.ElementType, x.Length) == input.Bytes.Length
                    )
                )
                {
                    continue;
                }
                var bytes = new byte[input.Bytes.Length];
                instance.GetMemory(input.MemoryExport).Read(input.Offset, bytes);
                if (!bytes.AsSpan().SequenceEqual(input.Bytes.AsSpan()))
                {
                    return new(
                        false,
                        definition.CaseId,
                        stage,
                        $"input.{input.MemoryExport}[{input.Offset}..{(ulong)input.Offset + (uint)bytes.Length}]",
                        Convert.ToHexStringLower(SHA256.HashData(input.Bytes.AsSpan())),
                        Convert.ToHexStringLower(SHA256.HashData(bytes))
                    );
                }
            }
        }
        foreach (var global in definition.ExpectedResult.Globals)
        {
            var actual = Describe(instance.GetGlobal(global.ExportName));
            var expected = $"{global.Value.Type}:{global.Value.Bits}";
            if (actual != expected)
            {
                return new(
                    false,
                    definition.CaseId,
                    stage,
                    $"global.{global.ExportName}",
                    expected,
                    actual
                );
            }
        }
        foreach (var table in definition.ExpectedResult.Tables)
        {
            var actual = instance.GetTable(table.ExportName);
            if (actual.ElementType != WasmValueKind.FuncRef)
            {
                return new(
                    false,
                    definition.CaseId,
                    stage,
                    $"table.{table.ExportName}.type",
                    nameof(WasmValueKind.FuncRef),
                    actual.ElementType.ToString()
                );
            }
            if (actual.Count != table.Length)
            {
                return new(
                    false,
                    definition.CaseId,
                    stage,
                    $"table.{table.ExportName}.count",
                    table.Length.ToString(),
                    actual.Count.ToString()
                );
            }
            foreach (var element in table.Elements)
            {
                var wanted = element.FunctionExport is null
                    ? null
                    : instance.GetFunction(element.FunctionExport);
                var observed = actual.Get(element.Index).AsFuncRef();
                if (!ReferenceEquals(wanted, observed))
                {
                    return new(
                        false,
                        definition.CaseId,
                        stage,
                        $"table.{table.ExportName}[{element.Index}]",
                        element.FunctionExport ?? "null",
                        observed is null ? "null" : "異なる関数実体"
                    );
                }
            }
        }
        return null;
    }

    private static int ByteLength(ArrayElementType type, uint length)
    {
        var elementSize = type switch
        {
            ArrayElementType.U8 => 1,
            ArrayElementType.U32 => sizeof(uint),
            _ => throw new InvalidDataException($"配列要素型'{type}'は未対応です。"),
        };
        return checked((int)((ulong)length * (uint)elementSize));
    }

    private static VerificationResult VerifyResults(BenchmarkCase definition, WasmResults results)
    {
        var expected = definition.ExpectedResult.Values;
        if (expected.Count != results.Values.Length)
        {
            return new(
                false,
                definition.CaseId,
                "Invoke",
                "results.count",
                expected.Count.ToString(),
                results.Values.Length.ToString()
            );
        }
        for (var index = 0; index < expected.Count; index++)
        {
            var actual = Describe(results.Values[index]);
            var wanted = $"{expected[index].Type}:{expected[index].Bits}";
            if (actual != wanted)
            {
                return new(false, definition.CaseId, "Invoke", $"results[{index}]", wanted, actual);
            }
        }
        return new(true, definition.CaseId, "Invoke", null, null, null);
    }

    private static string Describe(WasmValue value) =>
        value.Kind switch
        {
            WasmValueKind.I32 => $"I32:{unchecked((uint)value.AsI32()):x8}",
            WasmValueKind.I64 => $"I64:{unchecked((ulong)value.AsI64()):x16}",
            WasmValueKind.F32 => $"F32:{value.AsF32Bits():x8}",
            WasmValueKind.F64 => $"F64:{value.AsF64Bits():x16}",
            _ => value.Kind.ToString(),
        };
}
