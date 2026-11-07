using System.Collections.Immutable;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseVerifier_VerifyTests
{
    [Test]
    [Arguments("Decode")]
    [Arguments("Validate")]
    [Arguments("Instantiate")]
    public async Task 読込済み素材が公開操作で失敗する_ケースと失敗段階と例外を保持する(
        string failedStage
    )
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.single");
        // Decodeは空入力、Validateはi32結果の欠落、Instantiateは未提供importで失敗する
        ImmutableArray<byte> bytes = failedStage switch
        {
            "Decode" => [],
            "Validate" =>
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
                10,
                4,
                1,
                2,
                0,
                0x0b,
            ],
            _ =>
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
                4,
                1,
                0x60,
                0,
                0,
                2,
                7,
                1,
                1,
                0x78,
                1,
                0x78,
                0,
                0,
            ],
        };
        var assets = InvokeFixture.BuildAssets(definition, bytes);

        // Act
        var result = CaseVerifier.Verify(assets, definition.CaseId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo(failedStage);
            await Assert.That(result.Subject).IsEqualTo("exception");
            await Assert.That(result.Expected).IsEqualTo("正常終了");
            await Assert.That(result.Observed).Contains("Exception");
        }
    }

    [Test]
    public async Task 読込済み素材の入力がdataセグメントと異なる_Invoke前に固定入力をmemoryへ書き込む()
    {
        // Arrange
        var definition = VerificationFixture.ChangedMemoryInputDefinition(MeasurementStage.Invoke);
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
        var assets = InvokeFixture.BuildAssets(definition, bytes);

        // Act
        var result = CaseVerifier.Verify(assets, definition.CaseId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo("Invoke");
        }
    }

    [Test]
    public async Task 追加Invoke用の入力が初期memoryと異なる_書込後の状態不一致を報告する()
    {
        // Arrange
        var definition = VerificationFixture.ChangedMemoryInputDefinition(
            MeasurementStage.Instantiate
        );
        var instance = VerificationFixture.Instantiate();

        // Act
        var result = CaseVerifier.Verify(definition, new InstanceObservation(instance));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("memory.memory[0..8]");
            await Assert
                .That(result.Expected)
                .IsEqualTo("8a851ff82ee7048ad09ec3847f1ddf44944104d2cbd17ef4e3db22c6785a0d45");
            await Assert
                .That(result.Observed)
                .IsEqualTo("ed0b4dd158a46dc424a660c0e9aaa52558406cb7c106391897bb07fd8dc3ba99");
        }
    }

    [Test]
    public async Task 期待配列のbyte数がuintの範囲を超える_空配列へ折り返して一致にしない()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Invoke) with
        {
            ExpectedResult = new(
                [],
                [
                    new(
                        "memory",
                        0,
                        ArrayElementType.U32,
                        1U << 30,
                        "sha256",
                        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                    ),
                ],
                [],
                []
            ),
        };
        var observation = new InvokeObservation(
            VerificationFixture.Instantiate(),
            new WasmResults([])
        );

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("exception");
            await Assert.That(result.Observed).Contains("System.OverflowException");
        }
    }

    [Test]
    [Arguments(MeasurementStage.Invoke)]
    [Arguments(MeasurementStage.Instantiate)]
    public async Task Invokeの戻り値と状態がともに異なる_戻り値の不一致を先に報告する(
        MeasurementStage stage
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(stage);
        definition = AssetLoadingFixture.WithInput(
            definition with
            {
                ExpectedResult = definition.ExpectedResult with
                {
                    Values = [new(NumericType.I32, "00000012")],
                    Globals = [new("start_result", new(NumericType.I32, "ffffffff"))],
                },
            },
            definition.Input with
            {
                FunctionExport = "repair",
            }
        );
        var instance = VerificationFixture.Instantiate();
        instance.GetGlobalResource("start_result").Value = WasmValue.FromI32(-1);
        CaseObservation observation =
            stage == MeasurementStage.Invoke
                ? new InvokeObservation(
                    instance,
                    instance.GetFunction("repair").Invoke([WasmValue.FromI32(17)])
                )
                : new InstanceObservation(instance);

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("results[0]");
            await Assert.That(result.Expected).IsEqualTo("I32:00000012");
            await Assert.That(result.Observed).IsEqualTo("I32:00000011");
            await Assert.That(instance.GetGlobal("start_result").AsI32()).IsEqualTo(42);
        }
    }

    [Test]
    public async Task 追加Invokeの戻り値は正しいが状態が変わる_Invoke後の状態不一致を報告する()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Instantiate);
        definition = AssetLoadingFixture.WithInput(
            definition with
            {
                ExpectedResult = definition.ExpectedResult with
                {
                    Globals = [new("start_result", new(NumericType.I32, "ffffffff"))],
                },
            },
            definition.Input with
            {
                FunctionExport = "repair",
            }
        );
        var instance = VerificationFixture.Instantiate();
        instance.GetGlobalResource("start_result").Value = WasmValue.FromI32(-1);

        // Act
        var result = CaseVerifier.Verify(definition, new InstanceObservation(instance));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("global.start_result");
            await Assert.That(result.Expected).IsEqualTo("I32:ffffffff");
            await Assert.That(result.Observed).IsEqualTo("I32:0000002a");
        }
    }

    [Test]
    public async Task funcrefを期待するtableがexternrefである_型の不一致を失敗にする()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Invoke) with
        {
            ExpectedResult = new([], [], [], [new("functions", 1, [])]),
        };
        // 要素数1のexternref tableだけをfunctionsとしてexportするmodule
        var instance = WasmModule
            .Decode([
                0,
                0x61,
                0x73,
                0x6d,
                1,
                0,
                0,
                0,
                4,
                4,
                1,
                0x6f,
                0,
                1,
                7,
                13,
                1,
                9,
                0x66,
                0x75,
                0x6e,
                0x63,
                0x74,
                0x69,
                0x6f,
                0x6e,
                0x73,
                1,
                0,
            ])
            .Validate()
            .Instantiate(new WasmImports());

        // Act
        var result = CaseVerifier.Verify(
            definition,
            new InvokeObservation(instance, new WasmResults([]))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("table.functions.type");
            await Assert.That(result.Expected).IsEqualTo("FuncRef");
            await Assert.That(result.Observed).IsEqualTo("ExternRef");
        }
    }

    [Test]
    [Arguments(MeasurementStage.Decode)]
    [Arguments(MeasurementStage.DecodeValidate)]
    [Arguments(MeasurementStage.Instantiate)]
    public async Task 読み込んだ素材の準備段階を検証する_指定段階から追加操作して固定値と照合する(
        MeasurementStage stage
    )
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        fixture.Definitions[0] = fixture.Definitions[0] with { MeasurementStage = stage };
        fixture.SaveCases();
        var assets = AssetManifest.Load(fixture.ManifestPath);

        // Act
        var result = CaseVerifier.Verify(assets, "invoke.single");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.CaseId).IsEqualTo("invoke.single");
            await Assert.That(result.Stage).IsEqualTo("Invoke");
        }
    }

    [Test]
    [Arguments(MeasurementStage.Decode, nameof(ModuleObservation))]
    [Arguments(MeasurementStage.Instantiate, nameof(InstanceObservation))]
    [Arguments(MeasurementStage.Invoke, nameof(InvokeObservation))]
    public async Task 測定段階と観測型が対応しない_必要な観測型を示して失敗にする(
        MeasurementStage stage,
        string expectedType
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(stage);
        CaseObservation observation =
            stage == MeasurementStage.Decode
                ? new InstanceObservation(VerificationFixture.Instantiate())
                : new ModuleObservation(VerificationFixture.Decode());

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo(stage.ToString());
            await Assert.That(result.Subject).IsEqualTo("observation");
            await Assert.That(result.Expected).IsEqualTo(expectedType);
            await Assert.That(result.Observed).IsEqualTo(observation.GetType().Name);
        }
    }

    [Test]
    public async Task 配列digestの方式が未対応である_方式を示して照合失敗にする()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Instantiate);
        definition = definition with
        {
            ExpectedResult = definition.ExpectedResult with
            {
                Arrays = [definition.ExpectedResult.Arrays[0] with { HashAlgorithm = "sha512" }],
            },
        };
        var observation = new InstanceObservation(VerificationFixture.Instantiate());

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Instantiate");
            await Assert.That(result.Subject).IsEqualTo("hash_algorithm");
            await Assert.That(result.Expected).IsEqualTo("sha256");
            await Assert.That(result.Observed).IsEqualTo("sha512");
        }
    }

    [Test]
    public async Task 入力配列のmemoryが存在しない_入力準備の失敗をInvoke段階として保持する()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var definition = fixture.Definitions[0];
        fixture.Definitions[0] = AssetLoadingFixture.WithInput(
            definition,
            definition.Input with
            {
                Arrays =
                [
                    new(
                        "missing",
                        0,
                        ArrayElementType.U8,
                        1,
                        ArrayInputPattern.IndexModulo256,
                        null,
                        null
                    ),
                ],
            }
        );
        fixture.SaveCases();
        var loaded = AssetManifest.Load(fixture.ManifestPath);

        // Act
        var result = CaseVerifier.Verify(loaded, definition.CaseId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("exception");
            await Assert.That(result.Observed).Contains("System.ArgumentException");
        }
    }

    [Test]
    [Arguments(MeasurementStage.Decode)]
    [Arguments(MeasurementStage.DecodeValidate)]
    [Arguments(MeasurementStage.Instantiate)]
    public async Task 追加Invokeの引数が不正_入力生成の失敗をInvoke段階として保持する(
        MeasurementStage stage
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(stage);
        definition = AssetLoadingFixture.WithInput(
            definition,
            definition.Input with
            {
                Arguments = [new(NumericType.I32, "invalid")],
            }
        );
        var module = VerificationFixture.Decode();
        CaseObservation observation = stage switch
        {
            MeasurementStage.Decode => new ModuleObservation(module),
            MeasurementStage.DecodeValidate => new ModuleObservation(module.Validate()),
            _ => new InstanceObservation(
                module.Validate().Instantiate(CaseVerifier.CreateImports())
            ),
        };

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo("exception");
            await Assert.That(result.Observed).Contains("System.IO.InvalidDataException");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 出力を上書きする操作を反復する_全出力を照合し破壊した末尾も検出する(
        bool matrix
    )
    {
        // Arrange
        var definition = VerificationFixture.MemoryDefinition(matrix);
        if (!matrix)
        {
            definition = AssetLoadingFixture.WithInput(
                definition,
                definition.Input with
                {
                    Arrays = definition
                        .Input.Arrays.Select(x => x.Offset == 16 ? x with { FillByte = 0 } : x)
                        .ToList(),
                }
            );
        }
        var input = InputFactory.Create(definition);
        var instance = VerificationFixture.Instantiate();
        foreach (var array in input.Arrays)
        {
            instance.GetMemory(array.MemoryExport).Write(array.Offset, array.Bytes.AsSpan());
        }
        var function = instance.GetFunction(input.FunctionExport);

        // Act & Assert
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var values = function.Invoke(input.Arguments.AsSpan());
            var result = CaseVerifier.Verify(definition, new InvokeObservation(instance, values));
            await Assert.That(result.Succeeded).IsTrue();
            var end = matrix ? 23U : 31U;
            instance.GetMemory("memory").Write(end, new byte[] { 0x80 });
            var changed = CaseVerifier.Verify(definition, new InvokeObservation(instance, values));
            using (Assert.Multiple())
            {
                await Assert.That(changed.Succeeded).IsFalse();
                await Assert.That(changed.Subject).Contains("memory.memory[");
                var untouched = new byte[1];
                instance.GetMemory("memory").Read(end, untouched);
                await Assert.That(untouched[0]).IsEqualTo((byte)0x80);
            }
        }
    }

    [Test]
    public async Task 行列の出力は正しいが固定入力が変わる_入力を復元せず反復条件の不一致を返す()
    {
        // Arrange
        var definition = VerificationFixture.MemoryDefinition(matrix: true);
        var instance = VerificationFixture.Instantiate();
        var values = instance.GetFunction("matrix").Invoke([]);
        instance.GetMemory("memory").Write(7, new byte[] { 0x80 });

        // Act
        var result = CaseVerifier.Verify(definition, new InvokeObservation(instance, values));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Subject).IsEqualTo("input.memory[0..8]");
            await Assert
                .That(result.Expected)
                .IsEqualTo("8a851ff82ee7048ad09ec3847f1ddf44944104d2cbd17ef4e3db22c6785a0d45");
            var source = new byte[1];
            instance.GetMemory("memory").Read(7, source);
            await Assert.That(source[0]).IsEqualTo((byte)0x80);
        }
    }

    [Test]
    [Arguments(MeasurementStage.Decode)]
    [Arguments(MeasurementStage.DecodeValidate)]
    [Arguments(MeasurementStage.Instantiate)]
    public async Task 準備段階の観測を渡す_追加Invokeと初期資源とstart結果が一致する(
        MeasurementStage stage
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(stage);
        var module = VerificationFixture.Decode();
        CaseObservation observation = stage switch
        {
            MeasurementStage.Decode => new ModuleObservation(module),
            MeasurementStage.DecodeValidate => new ModuleObservation(module.Validate()),
            _ => new InstanceObservation(
                module.Validate().Instantiate(CaseVerifier.CreateImports())
            ),
        };

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    [Arguments("memory", "memory.memory[0..8]")]
    [Arguments("global", "global.start_result")]
    [Arguments("table-null", "table.functions[2]")]
    [Arguments("table-function", "table.functions[0]")]
    [Arguments("table-missing-function", "table.functions[0]")]
    [Arguments("table-count", "table.functions.count")]
    public async Task 反復後の資源を変える_期待値と異なるmemoryとglobalとtableを失敗にする(
        string difference,
        string expectedSubject
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Invoke);
        var instance = VerificationFixture.Instantiate();
        switch (difference)
        {
            case "memory":
                instance.GetMemory("memory").Write(7, new byte[] { 0xff });
                break;
            case "global":
                instance.GetGlobalResource("start_result").Value = WasmValue.FromI32(-1);
                break;
            case "table-null":
                instance
                    .GetTable("functions")
                    .Set(2, WasmValue.FromFuncRef(instance.GetFunction("probe")));
                break;
            case "table-function":
                instance
                    .GetTable("functions")
                    .Set(
                        0,
                        WasmValue.FromFuncRef(
                            VerificationFixture.Instantiate().GetFunction("probe")
                        )
                    );
                break;
            case "table-missing-function":
                instance.GetTable("functions").Set(0, WasmValue.FromFuncRef(null));
                break;
            case "table-count":
                instance.GetTable("functions").TryGrow(1, WasmValue.FromFuncRef(null), out _);
                break;
        }

        // Act
        var result = CaseVerifier.Verify(
            definition,
            new InvokeObservation(instance, new([WasmValue.FromI32(17)]))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo(expectedSubject);
            await Assert.That(result.Expected == result.Observed).IsFalse();
            if (difference is "table-null" or "table-function" or "table-missing-function")
            {
                await Assert
                    .That(result.Expected)
                    .IsEqualTo(difference == "table-null" ? "null" : "probe");
                await Assert
                    .That(result.Observed)
                    .IsEqualTo(difference == "table-missing-function" ? "null" : "異なる関数実体");
            }
        }
    }

    [Test]
    public async Task Instantiate後のstart結果が壊れている_追加Invokeで修復する前に失敗を返す()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Instantiate);
        definition = AssetLoadingFixture.WithInput(
            definition,
            definition.Input with
            {
                FunctionExport = "repair",
            }
        );
        var instance = VerificationFixture.Instantiate();
        instance.GetGlobalResource("start_result").Value = WasmValue.FromI32(-1);

        // Act
        var result = CaseVerifier.Verify(definition, new InstanceObservation(instance));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Instantiate");
            await Assert.That(result.Subject).IsEqualTo("global.start_result");
            await Assert.That(result.Expected).IsEqualTo("I32:0000002a");
            await Assert.That(result.Observed).IsEqualTo("I32:ffffffff");
            await Assert.That(instance.GetGlobal("start_result").AsI32()).IsEqualTo(-1);
        }
    }

    [Test]
    [Arguments(MeasurementStage.DecodeValidate, "Instantiate")]
    [Arguments(MeasurementStage.Decode, "Validate")]
    public async Task 追加操作が失敗する_実際に失敗した公開処理段階と例外を保持する(
        MeasurementStage stage,
        string failedStage
    )
    {
        // Arrange
        var definition = VerificationFixture.Definition(stage);
        var bytes = new byte[]
        {
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
            10,
            4,
            1,
            2,
            0,
            0x0b,
        };
        var module =
            stage == MeasurementStage.Decode
                ? WasmModule.Decode(bytes)
                : VerificationFixture.Decode();

        // Act
        var result = CaseVerifier.Verify(definition, new ModuleObservation(module));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo(definition.CaseId);
            await Assert.That(result.Stage).IsEqualTo(failedStage);
            await Assert.That(result.Subject).IsEqualTo("exception");
            await Assert.That(result.Expected).IsEqualTo("正常終了");
            await Assert.That(result.Observed).Contains("Exception");
        }
    }

    [Test]
    public async Task 追加Invokeでtrapになる_ケースとInvoke段階とtrapを保持する()
    {
        // Arrange
        var definition = VerificationFixture.Definition(MeasurementStage.Instantiate);
        definition = AssetLoadingFixture.WithInput(definition, new("trap", [], []));

        // Act
        var result = CaseVerifier.Verify(
            definition,
            new InstanceObservation(VerificationFixture.Instantiate())
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Observed).Contains("WasmTrapException");
        }
    }

    [Test]
    public async Task Decodeしたmoduleを渡す_計時外の追加操作で固定結果を照合する()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "decode.small");
        var module = WasmModule.Decode(
            File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "Loading", "invoke.wasm")
            )
        );

        // Act
        var result = CaseVerifier.Verify(definition, new ModuleObservation(module));

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    [Arguments("count", "results.count")]
    [Arguments("order", "results[0]")]
    [Arguments("type", "results[2]")]
    [Arguments("bits", "results[2]")]
    public async Task 結果を意図的に変える_ケースと段階と期待と観測を持つ失敗になる(
        string difference,
        string expectedSubject
    )
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.multi");
        var values = new List<WasmValue>
        {
            WasmValue.FromI32(unchecked((int)0x89abcdef)),
            WasmValue.FromI64(unchecked((long)0xfedcba9876543210)),
            WasmValue.FromF64Bits(0x8000000000000000UL),
        };
        switch (difference)
        {
            case "count":
                values.RemoveAt(2);
                break;
            case "order":
                (values[0], values[1]) = (values[1], values[0]);
                break;
            case "type":
                values[2] = WasmValue.FromI64(long.MinValue);
                break;
            case "bits":
                values[2] = WasmValue.FromF64Bits(0);
                break;
        }
        var instance = EmptyInstance();

        // Act
        var result = CaseVerifier.Verify(
            definition,
            new InvokeObservation(instance, new WasmResults(values.ToArray()))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.CaseId).IsEqualTo("invoke.multi");
            await Assert.That(result.Stage).IsEqualTo("Invoke");
            await Assert.That(result.Subject).IsEqualTo(expectedSubject);
            await Assert.That(result.Expected).IsNotNull();
            await Assert.That(result.Observed).IsNotNull();
            await Assert.That(result.Expected == result.Observed).IsFalse();
        }
    }

    [Test]
    public async Task ゼロ結果と浮動小数点の特殊bit列を渡す_個数とNaNのpayloadを厳密に照合する()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.empty");
        var instance = EmptyInstance();
        var special = definition with
        {
            ExpectedResult = new(
                [new(NumericType.F32, "7fc12345"), new(NumericType.F64, "fff8000000000042")],
                [],
                [],
                []
            ),
        };

        // Act
        var empty = CaseVerifier.Verify(
            definition,
            new InvokeObservation(instance, new WasmResults([]))
        );
        var same = CaseVerifier.Verify(
            special,
            new InvokeObservation(
                instance,
                new WasmResults([
                    WasmValue.FromF32Bits(0x7fc12345),
                    WasmValue.FromF64Bits(0xfff8000000000042),
                ])
            )
        );
        var different = CaseVerifier.Verify(
            special,
            new InvokeObservation(
                instance,
                new WasmResults([
                    WasmValue.FromF32Bits(0x7fc12346),
                    WasmValue.FromF64Bits(0xfff8000000000042),
                ])
            )
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(empty.Succeeded).IsTrue();
            await Assert.That(same.Succeeded).IsTrue();
            await Assert.That(different.Succeeded).IsFalse();
            await Assert.That(different.Expected).IsEqualTo("F32:7fc12345");
            await Assert.That(different.Observed).IsEqualTo("F32:7fc12346");
        }
    }

    [Test]
    public async Task 固定Invokeの観測を渡す_期待する結果の個数と順序とbit列が一致する()
    {
        // Arrange
        var definition = AssetLoadingFixture.ReadCases().Single(x => x.CaseId == "invoke.multi");
        var instance = WasmModule
            .Decode([0, 0x61, 0x73, 0x6d, 1, 0, 0, 0])
            .Validate()
            .Instantiate(new WasmImports());
        var observation = new InvokeObservation(
            instance,
            new WasmResults([
                WasmValue.FromI32(unchecked((int)0x89abcdef)),
                WasmValue.FromI64(unchecked((long)0xfedcba9876543210)),
                WasmValue.FromF64Bits(0x8000000000000000UL),
            ])
        );

        // Act
        var result = CaseVerifier.Verify(definition, observation);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    private static WasmInstance EmptyInstance() =>
        WasmModule
            .Decode([0, 0x61, 0x73, 0x6d, 1, 0, 0, 0])
            .Validate()
            .Instantiate(new WasmImports());
}
