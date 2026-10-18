using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(0, WasmValueKind.FuncRef)]
    [Arguments(0, WasmValueKind.ExternRef)]
    [Arguments(1, WasmValueKind.FuncRef)]
    [Arguments(1, WasmValueKind.ExternRef)]
    [Arguments(2, WasmValueKind.FuncRef)]
    [Arguments(2, WasmValueKind.ExternRef)]
    public async Task 各modeの要素式がimport不変globalの参照型と一致する_検証に成功する(
        int mode,
        WasmValueKind kind
    )
    {
        // Arrange
        var initializer = ElementExpression(DecodedInstruction.WithIndex(new(0, 0x23), 0, 52));
        var element = CreateValidationElement(mode, kind, [initializer]);
        var module = CreateElementValidationModule(
            [element],
            mode == 0 ? [new(kind, new(0, null), 30)] : [],
            [new GlobalImport("env", "reference", new(kind, false), 20)]
        );

        // Act
        var validated = module.Validate();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task passiveまたはdeclarativeの要素列が空_テーブルなしでも検証に成功する(int mode)
    {
        // Arrange
        var module = CreateElementValidationModule(
            [CreateValidationElement(mode, WasmValueKind.FuncRef, [])],
            []
        );

        // Act & Assert
        await Assert.That(() => module.Validate()).ThrowsNothing();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task 各modeの要素式が数値を返す_要素セクションの式終端で型不一致になる(int mode)
    {
        // Arrange
        var element = CreateValidationElement(
            mode,
            WasmValueKind.FuncRef,
            [
                ElementExpression(
                    DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(0), 52)
                ),
            ]
        );
        var module = CreateElementValidationModule([element], []);

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
                .IsEqualTo(new(WasmProcessingStage.Validate, 54, null, 9));
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task 各modeの要素式に非定数命令がある_命令位置で定数式違反になる(int mode)
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                CreateValidationElement(
                    mode,
                    WasmValueKind.FuncRef,
                    [ElementExpression(DecodedInstruction.WithoutImmediate(new(0, 0x01), 52))]
                ),
            ],
            []
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
                .IsEqualTo(new(WasmProcessingStage.Validate, 52, null, 9));
        }
    }

    [Test]
    public async Task activeの後続要素式と対象tableがともに不正_全要素式の検証をtable検査より優先する()
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                CreateValidationElement(
                    0,
                    WasmValueKind.FuncRef,
                    [
                        ElementExpression(DecodedInstruction.WithIndex(new(0, 0x23), 0, 52)),
                        ElementExpression(
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(0),
                                62
                            ),
                            64
                        ),
                    ]
                ),
            ],
            [],
            [new GlobalImport("env", "reference", new(WasmValueKind.FuncRef, false), 20)]
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
                .IsEqualTo(new(WasmProcessingStage.Validate, 64, null, 9));
        }
    }

    [Test]
    [Arguments(0u)]
    [Arguments(uint.MaxValue)]
    public async Task activeのtableとoffsetがともに不正_tableの存在を先に検査する(uint tableIndex)
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                ElementDefinition.Active(
                    WasmValueKind.FuncRef,
                    [],
                    new(
                        tableIndex,
                        ElementExpression(
                                DecodedInstruction.WithImmediate(
                                    new(0, 0x42),
                                    WasmValue.FromI64(0),
                                    72
                                ),
                                74
                            )
                            .AsSpan()
                    ),
                    50
                ),
            ],
            []
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
                        $"unknown table {tableIndex}",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 50, null, 9));
        }
    }

    [Test]
    public async Task activeのtable参照型とoffsetがともに不正_参照型の照合を先に行う()
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                ElementDefinition.Active(
                    WasmValueKind.FuncRef,
                    [],
                    new(
                        0,
                        ElementExpression(
                                DecodedInstruction.WithImmediate(
                                    new(0, 0x42),
                                    WasmValue.FromI64(0),
                                    72
                                ),
                                74
                            )
                            .AsSpan()
                    ),
                    50
                ),
            ],
            [new(WasmValueKind.ExternRef, new(0, null), 30)]
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
                .IsEqualTo(new(WasmProcessingStage.Validate, 50, null, 9));
        }
    }

    [Test]
    public async Task activeのoffsetがI64を返す_要素セクションのoffset終端で型不一致になる()
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                ElementDefinition.Active(
                    WasmValueKind.FuncRef,
                    [],
                    new(
                        0,
                        ElementExpression(
                                DecodedInstruction.WithImmediate(
                                    new(0, 0x42),
                                    WasmValue.FromI64(0),
                                    72
                                ),
                                74
                            )
                            .AsSpan()
                    ),
                    50
                ),
            ],
            [new(WasmValueKind.FuncRef, new(0, null), 30)]
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
                .IsEqualTo(new(WasmProcessingStage.Validate, 74, null, 9));
        }
    }

    [Test]
    [Arguments(0u, WasmValueKind.ExternRef)]
    [Arguments(1u, WasmValueKind.FuncRef)]
    public async Task activeがimportまたは定義tableを参照する_importを先頭とした型列で検証する(
        uint tableIndex,
        WasmValueKind kind
    )
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                ElementDefinition.Active(
                    kind,
                    [],
                    new(
                        tableIndex,
                        ElementExpression(DecodedInstruction.WithIndex(new(0, 0x23), 0, 72), 74)
                            .AsSpan()
                    ),
                    50
                ),
            ],
            [new(WasmValueKind.FuncRef, new(0, null), 30)],
            [
                new GlobalImport("env", "offset", new(WasmValueKind.I32, false), 10),
                new TableImport("env", "table", new(WasmValueKind.ExternRef, new(0, null), 20), 20),
            ]
        );

        // Act & Assert
        await Assert.That(() => module.Validate()).ThrowsNothing();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素が不正_後続データの有無によらず要素の診断を報告し再検証でも成功状態にしない(
        bool invalidData
    )
    {
        // Arrange
        var module = CreateElementValidationModule(
            [
                CreateValidationElement(
                    1,
                    WasmValueKind.FuncRef,
                    [
                        ElementExpression(
                            DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(0), 52)
                        ),
                    ]
                ),
            ],
            [],
            data: invalidData
                ?
                [
                    new(
                        [],
                        new(
                            0,
                            ElementExpression(
                                    DecodedInstruction.WithImmediate(
                                        new(0, 0x41),
                                        WasmValue.FromI32(0),
                                        82
                                    ),
                                    84
                                )
                                .AsSpan()
                        ),
                        80
                    ),
                ]
                : []
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Location!.SectionId).IsEqualTo((byte)9);
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }

    private static ImmutableArray<DecodedInstruction> ElementExpression(
        DecodedInstruction instruction,
        long endOffset = 54
    )
    {
        return [instruction, DecodedInstruction.WithoutImmediate(new(0, 0x0B), endOffset)];
    }

    private static ElementDefinition CreateValidationElement(
        int mode,
        WasmValueKind kind,
        ReadOnlySpan<IEnumerable<DecodedInstruction>> initializers
    )
    {
        return mode switch
        {
            0 => ElementDefinition.Active(
                kind,
                initializers,
                new(
                    0,
                    ElementExpression(
                            DecodedInstruction.WithImmediate(
                                new(0, 0x41),
                                WasmValue.FromI32(0),
                                72
                            ),
                            74
                        )
                        .AsSpan()
                ),
                50
            ),
            1 => ElementDefinition.Passive(kind, initializers, 50),
            2 => ElementDefinition.Declarative(kind, initializers, 50),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static WasmModule CreateElementValidationModule(
        ReadOnlySpan<ElementDefinition> elements,
        ReadOnlySpan<TableDefinition> tables,
        ReadOnlySpan<ModuleImport> imports = default,
        ReadOnlySpan<DataDefinition> data = default
    )
    {
        return new(
            [new([], [])],
            [new(0, 40, [], [DecodedInstruction.WithoutImmediate(new(0, 0x0B), 41)])],
            [new("run", 0, 35, WasmExternalKind.Function)],
            100,
            imports,
            tables,
            [],
            [],
            null,
            dataSegments: data,
            elementSegments: elements
        );
    }
}
