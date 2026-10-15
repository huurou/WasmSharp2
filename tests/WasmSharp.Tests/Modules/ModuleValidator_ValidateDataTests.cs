using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(0u)]
    [Arguments(uint.MaxValue)]
    public async Task アクティブデータが存在しないメモリを参照する_式より先にセグメント位置で拒否する(
        uint memoryIndex
    )
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [],
            100,
            [],
            [],
            [],
            [],
            null,
            [
                new(
                    [0x41],
                    new(
                        memoryIndex,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x42),
                                WasmValue.FromI64(0),
                                52
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        $"unknown memory {memoryIndex}",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 50, null, 11));
        }
    }

    [Test]
    public async Task データのオフセット式がI64を返す_データセクションの終端位置で型不一致になる()
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [],
            100,
            [],
            [],
            [new(new(0, null), 30)],
            [],
            null,
            [
                new(
                    [],
                    new(
                        0,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x42),
                                WasmValue.FromI64(0),
                                52
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 54, null, 11));
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task アクティブデータのメモリ参照とI32定数式が有効_書き込み範囲を調べず検証済みにする(
        bool importedMemory,
        bool useGlobal
    )
    {
        // Arrange
        ModuleImport[] imports =
        [
            new GlobalImport("env", "unused", new(WasmValueKind.I64, false), 20),
            .. importedMemory
                ? new ModuleImport[]
                {
                    new MemoryImport("env", "memory", new(new(0, null), 25), 25),
                }
                : [],
            new GlobalImport("env", "offset", new(WasmValueKind.I32, false), 30),
        ];
        var module = new WasmModule(
            [new([], [])],
            [new(0, 40, [], [DecodedInstruction.WithoutImmediate(new(0, 0x0B), 41)])],
            [new("run", 0, 35, WasmExternalKind.Function)],
            100,
            imports,
            [],
            importedMemory ? [] : [new(new(0, null), 32)],
            [],
            null,
            [
                new(
                    [0x41],
                    new(
                        0,
                        [
                            useGlobal
                                ? DecodedInstruction.WithIndex(new(0, 0x23), 1, 52)
                                : DecodedInstruction.WithImmediate(
                                    new(0, 0x41),
                                    WasmValue.FromI32(-1),
                                    52
                                ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act
        var validated = module.Validate();
        var codes = module.FunctionCodes;
        var validatedAgain = module.Validate();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(validated, module)).IsTrue();
            await Assert.That(ReferenceEquals(validatedAgain, module)).IsTrue();
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
            await Assert.That(module.FunctionCodes == codes).IsTrue();
            await Assert.That(module.FunctionExportIndices["run"]).IsEqualTo(0);
        }
    }

    [Test]
    public async Task パッシブデータだけを定義する_メモリなしで受理する()
    {
        // Arrange
        var module = new WasmModule([], [], [], 100, [], [], [], [], null, [new([0x41], null, 50)]);

        // Act & Assert
        await Assert.That(() => module.Validate()).ThrowsNothing();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task データのオフセット式が可変または定義済みglobalを参照する_命令位置で拒否する(
        bool mutableImport
    )
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [],
            100,
            [new GlobalImport("env", "offset", new(WasmValueKind.I32, mutableImport), 20)],
            [],
            [new(new(0, null), 30)],
            [
                new(
                    new(WasmValueKind.I32, false),
                    [
                        DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(0), 40),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
                    ],
                    39
                ),
            ],
            null,
            [
                new(
                    [],
                    new(
                        0,
                        [
                            DecodedInstruction.WithIndex(new(0, 0x23), mutableImport ? 0u : 1u, 52),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        mutableImport ? "constant expression required" : "unknown global 1",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 52, null, 11));
        }
    }

    [Test]
    public async Task データのオフセット式が数値命令を含む_結果個数より先に命令位置で定数式違反になる()
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [],
            100,
            [],
            [],
            [new(new(0, null), 30)],
            [],
            null,
            [
                new(
                    [],
                    new(
                        0,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(1),
                                52
                            ),
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(2),
                                54
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x6A), 56),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 57),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "constant expression required",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 56, null, 11));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task データと関数本体が不正_リソース検証後にデータを宣言順で検証する(
        bool invalidResource
    )
    {
        // Arrange
        var module = new WasmModule(
            [new([], [])],
            [
                new(
                    0,
                    40,
                    [],
                    [
                        DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(1), 41),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 43),
                    ]
                ),
            ],
            [],
            100,
            [],
            [],
            [new(new(invalidResource ? 1u : 0u, 0), 30)],
            [],
            null,
            [
                new([], null, 45),
                new(
                    [],
                    new(
                        0,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x42),
                                WasmValue.FromI64(0),
                                52
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
                new(
                    [],
                    new(
                        1,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(0),
                                62
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 64),
                        ]
                    ),
                    60
                ),
            ]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        invalidResource
                            ? "size minimum must not be greater than maximum"
                            : "type mismatch",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        invalidResource ? 30 : 54,
                        null,
                        invalidResource ? (byte)5 : (byte)11
                    )
                );
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task データまたは後続のexportが不正_再検証でも実行コードと検証成功状態を確定しない(
        bool invalidData
    )
    {
        // Arrange
        var module = new WasmModule(
            [new([], [])],
            [new(0, 40, [], [DecodedInstruction.WithoutImmediate(new(0, 0x0B), 41)])],
            [new("run", invalidData ? 0u : 1u, 35, WasmExternalKind.Function)],
            100,
            [],
            [],
            [new(new(0, null), 30)],
            [],
            null,
            [
                new(
                    [],
                    new(
                        invalidData ? 1u : 0u,
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(0),
                                52
                            ),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 54),
                        ]
                    ),
                    50
                ),
            ]
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(
                        exception!.Message.StartsWith(
                            invalidData ? "unknown memory 1" : "unknown function 1",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(exception.Location!.SectionId)
                    .IsEqualTo(invalidData ? (byte)11 : (byte)7);
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}
