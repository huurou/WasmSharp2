using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Tests.Modules;

internal class ReferenceValidationContext_CreateTests
{
    [Test]
    public async Task 関数とテーブルのimportが混在する_関数総数と添字順の型列を保持する()
    {
        // Arrange
        ModuleImport[] imports =
        [
            new TableImport("env", "first", new(WasmValueKind.ExternRef, new(0, null), 10), 10),
            new FunctionImport("env", "function", 0, 15),
            new GlobalImport("env", "global", new(WasmValueKind.I32, false), 20),
            new TableImport("env", "second", new(WasmValueKind.FuncRef, new(0, null), 25), 25),
            new FunctionImport("env", "other", 0, 30),
        ];
        TableDefinition[] tables =
        [
            new(WasmValueKind.FuncRef, new(0, null), 40),
            new(WasmValueKind.ExternRef, new(0, null), 45),
        ];
        ElementDefinition[] elements =
        [
            ElementDefinition.Passive(WasmValueKind.ExternRef, [], 50),
            ElementDefinition.Declarative(WasmValueKind.FuncRef, [], 55),
            ElementDefinition.Active(WasmValueKind.ExternRef, [], new(3, []), 60),
        ];
        var module = new WasmModule(
            [new([], [])],
            [new(0, 70, [], []), new(0, 75, [], [])],
            [],
            100,
            imports,
            tables,
            [],
            [],
            null,
            elementSegments: elements
        );

        // Act
        var references = ReferenceValidationContext.Create(module, 2);
        Array.Clear(imports);
        Array.Clear(tables);
        Array.Clear(elements);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(references.FunctionCount).IsEqualTo(4u);
            await Assert
                .That(
                    references.TableElementTypes.SequenceEqual([
                        WasmValueKind.ExternRef,
                        WasmValueKind.FuncRef,
                        WasmValueKind.FuncRef,
                        WasmValueKind.ExternRef,
                    ])
                )
                .IsTrue();
            await Assert
                .That(
                    references.ElementTypes.SequenceEqual([
                        WasmValueKind.ExternRef,
                        WasmValueKind.FuncRef,
                        WasmValueKind.ExternRef,
                    ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task global式と関数exportがある_重複を除いて未検証の関数添字を宣言する()
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [
                new("function", 3, 10, WasmExternalKind.Function),
                new("alias", 3, 15, WasmExternalKind.Function),
                new("unknown", uint.MaxValue, 20, WasmExternalKind.Function),
                new("table", 8, 25, WasmExternalKind.Table),
                new("memory", 9, 30, WasmExternalKind.Memory),
                new("global", 10, 35, WasmExternalKind.Global),
            ],
            100,
            [],
            [],
            [],
            [
                new(
                    new(WasmValueKind.FuncRef, false),
                    [
                        DecodedInstruction.WithIndex(new(0, 0xD2), 4, 41),
                        DecodedInstruction.WithIndex(new(0, 0xD2), 3, 43),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 45),
                    ],
                    40
                ),
            ],
            null
        );

        // Act
        var references = ReferenceValidationContext.Create(module, 0);

        // Assert
        await Assert.That(references.DeclaredFunctions).IsEquivalentTo([3u, 4u, uint.MaxValue]);
    }

    [Test]
    public async Task 全modeの要素式とactiveのoffsetがある_すべての関数参照を宣言する()
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
            elementSegments:
            [
                ElementDefinition.Active(
                    WasmValueKind.FuncRef,
                    [
                        new[] { DecodedInstruction.WithIndex(new(0, 0xD2), 1, 21) },
                        new[] { DecodedInstruction.WithIndex(new(0, 0xD2), 2, 23) },
                    ],
                    new(uint.MaxValue, [DecodedInstruction.WithIndex(new(0, 0xD2), 3, 25)]),
                    20
                ),
                ElementDefinition.Passive(
                    WasmValueKind.FuncRef,
                    [new[] { DecodedInstruction.WithIndex(new(0, 0xD2), 4, 41) }],
                    40
                ),
                ElementDefinition.Declarative(
                    WasmValueKind.FuncRef,
                    [new[] { DecodedInstruction.WithIndex(new(0, 0xD2), 5, 61) }],
                    60
                ),
            ]
        );

        // Act
        var references = ReferenceValidationContext.Create(module, 0);

        // Assert
        await Assert.That(references.DeclaredFunctions).IsEquivalentTo([1u, 2u, 3u, 4u, 5u]);
    }

    [Test]
    public async Task 定数式領域に非定数命令がある_直接callも収集し他の添字を宣言しない()
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
            [
                new(
                    new(WasmValueKind.I32, false),
                    [
                        DecodedInstruction.WithIndex(new(0, 0x10), 1, 21),
                        DecodedInstruction.WithIndexPair(new(0, 0x11), 2, 3, 23),
                        DecodedInstruction.WithIndex(new(0, 0x23), 4, 25),
                        DecodedInstruction.WithIndex(new(0xFC, 0xD2), 5, 27),
                    ],
                    20
                ),
            ],
            null,
            dataSegments:
            [
                new(
                    [],
                    new(
                        uint.MaxValue,
                        [
                            DecodedInstruction.WithIndex(new(0, 0xD2), uint.MaxValue, 41),
                            DecodedInstruction.WithIndex(new(0, 0x10), 6, 43),
                        ]
                    ),
                    40
                ),
                new([], null, 50),
            ],
            elementSegments:
            [
                ElementDefinition.Active(
                    WasmValueKind.ExternRef,
                    [new[] { DecodedInstruction.WithIndex(new(0, 0x10), 7, 61) }],
                    new(0, [DecodedInstruction.WithIndex(new(0, 0x10), 8, 63)]),
                    60
                ),
            ]
        );

        // Act
        var references = ReferenceValidationContext.Create(module, 0);

        // Assert
        await Assert
            .That(references.DeclaredFunctions)
            .IsEquivalentTo([1u, 6u, 7u, 8u, uint.MaxValue]);
    }

    [Test]
    public async Task 関数本体とstartとimportだけに関数がある_宣言済み集合に含めない()
    {
        // Arrange
        var module = new WasmModule(
            [new([], [])],
            [
                new(
                    0,
                    30,
                    [],
                    [
                        DecodedInstruction.WithIndex(new(0, 0xD2), 0, 31),
                        DecodedInstruction.WithIndex(new(0, 0x10), 1, 33),
                        DecodedInstruction.WithIndex(new(0, 0xD2), uint.MaxValue, 35),
                    ]
                ),
            ],
            [],
            100,
            [new FunctionImport("env", "function", 0, 20)],
            [],
            [],
            [],
            new(1, 40)
        );

        // Act
        var references = ReferenceValidationContext.Create(module, 1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(references.FunctionCount).IsEqualTo(2u);
            await Assert.That(references.DeclaredFunctions.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task moduleが空である_既定値でない空の不変情報を返す()
    {
        // Arrange
        var module = new WasmModule([], [], [], 8, [], [], [], [], null);

        // Act
        var references = ReferenceValidationContext.Create(module, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(references.FunctionCount).IsEqualTo(0u);
            await Assert.That(references.TableElementTypes.IsDefault).IsFalse();
            await Assert.That(references.TableElementTypes.IsEmpty).IsTrue();
            await Assert.That(references.ElementTypes.IsDefault).IsFalse();
            await Assert.That(references.ElementTypes.IsEmpty).IsTrue();
            await Assert.That(references.DeclaredFunctions.IsEmpty).IsTrue();
        }
    }
}
