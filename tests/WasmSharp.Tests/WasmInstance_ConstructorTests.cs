using WasmSharp.Execution;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_ConstructorTests
{
    [Test]
    public async Task 全modeの要素を持つ_関数とglobalの構築後に宣言順で評価して保持する()
    {
        // Arrange
        var external = new object();
        var importedGlobal = new WasmGlobal(
            new(WasmValueKind.ExternRef, false),
            WasmValue.FromExternRef(external)
        );
        var importedFunction = WasmFunction.CreateHost(new([], []), _ => new([]));
        var end = DecodedInstruction.WithoutImmediate(new(0, 0x0B), 1);
        DecodedInstruction[] definedReference =
        [
            DecodedInstruction.WithIndex(new(0, 0xD2), 1, 0),
            end,
        ];
        var module = new WasmModule(
            types: [new([], [])],
            functions: [new(0, 0, [], [end])],
            exports: [],
            inputLength: 0,
            imports:
            [
                new FunctionImport("env", "f", 0, 0),
                new GlobalImport("env", "g", importedGlobal.Type, 0),
                new TableImport("env", "t", new(WasmValueKind.FuncRef, new WasmLimits(2), 0), 0),
            ],
            tables: [],
            memories: [],
            globals: [new(new(WasmValueKind.FuncRef, false), definedReference, 0)],
            start: null,
            elementSegments:
            [
                ElementDefinition.Active(
                    WasmValueKind.FuncRef,
                    [definedReference, [DecodedInstruction.WithIndex(new(0, 0xD2), 0, 0), end]],
                    new(
                        0,
                        [
                            DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(0), 0),
                            end,
                        ]
                    ),
                    0
                ),
                ElementDefinition.Passive(
                    WasmValueKind.ExternRef,
                    [
                        [DecodedInstruction.WithIndex(new(0, 0x23), 0, 0), end],
                    ],
                    0
                ),
                ElementDefinition.Declarative(
                    WasmValueKind.FuncRef,
                    [
                        [
                            DecodedInstruction.WithImmediate(
                                new(0, 0xD0),
                                WasmValue.FromFuncRef(null),
                                0
                            ),
                            end,
                        ],
                    ],
                    0
                ),
            ]
        );
        var sharedTable = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(2));
        var externalTable = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1));
        var declaredTable = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(1));
        declaredTable.Set(0, WasmValue.FromFuncRef(importedFunction));

        // Act
        var instance = new WasmInstance(
            module,
            WasmExecutionOptions.Default,
            [importedFunction],
            [importedGlobal],
            [],
            [sharedTable]
        );
        var initialFirst = sharedTable.Get(0);
        var initialSecond = sharedTable.Get(1);
        var activeSuccess = instance.ElementSegments[0].TryInitialize(sharedTable, 0, 0, 2);
        var passiveSuccess = instance.ElementSegments[1].TryInitialize(externalTable, 0, 0, 1);
        var declarativeSuccess = instance.ElementSegments[2].TryInitialize(declaredTable, 0, 0, 1);

        // Assert
        await Assert.That(instance.ElementSegments.Length).IsEqualTo(3);
        using (Assert.Multiple())
        {
            await Assert.That(instance.ElementSegments[0].Length).IsEqualTo(2u);
            await Assert.That(instance.ElementSegments[1].Length).IsEqualTo(1u);
            await Assert.That(instance.ElementSegments[2].Length).IsEqualTo(1u);
            await Assert.That(initialFirst.AsFuncRef()).IsNull();
            await Assert.That(initialSecond.AsFuncRef()).IsNull();
            await Assert.That(activeSuccess).IsTrue();
            await Assert.That(passiveSuccess).IsTrue();
            await Assert.That(declarativeSuccess).IsTrue();
            await Assert
                .That(sharedTable.Get(0).AsFuncRef())
                .IsSameReferenceAs(instance.Functions[1]);
            await Assert.That(sharedTable.Get(1).AsFuncRef()).IsSameReferenceAs(importedFunction);
            await Assert
                .That(instance.Globals[1].Value.AsFuncRef())
                .IsSameReferenceAs(instance.Functions[1]);
            await Assert.That(externalTable.Get(0).AsExternRef()).IsSameReferenceAs(external);
            await Assert.That(declaredTable.Get(0).AsFuncRef()).IsNull();
        }
    }

    [Test]
    public async Task 同じmoduleから別instanceを構築する_要素の寿命と定義関数の所属instanceを分離する()
    {
        // Arrange
        var end = DecodedInstruction.WithoutImmediate(new(0, 0x0B), 1);
        var module = new WasmModule(
            types: [new([], [])],
            functions: [new(0, 0, [], [end])],
            exports: [],
            inputLength: 0,
            imports: [],
            tables: [],
            memories: [],
            globals: [],
            start: null,
            elementSegments:
            [
                ElementDefinition.Passive(
                    WasmValueKind.FuncRef,
                    [
                        [DecodedInstruction.WithIndex(new(0, 0xD2), 0, 0), end],
                    ],
                    0
                ),
            ]
        );
        var firstTable = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(1));
        var secondTable = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(1));

        // Act
        var first = new WasmInstance(module, WasmExecutionOptions.Default);
        var second = new WasmInstance(module, WasmExecutionOptions.Default);
        var firstSuccess = first.ElementSegments[0].TryInitialize(firstTable, 0, 0, 1);
        first.ElementSegments[0].Drop();
        var secondSuccess = second.ElementSegments[0].TryInitialize(secondTable, 0, 0, 1);

        // Assert
        await Assert.That(first.ElementSegments.Length).IsEqualTo(1);
        await Assert.That(second.ElementSegments.Length).IsEqualTo(1);
        using (Assert.Multiple())
        {
            await Assert
                .That(ReferenceEquals(first.ElementSegments[0], second.ElementSegments[0]))
                .IsFalse();
            await Assert.That(first.ElementSegments[0].Length).IsEqualTo(0u);
            await Assert.That(second.ElementSegments[0].Length).IsEqualTo(1u);
            await Assert.That(firstSuccess).IsTrue();
            await Assert.That(secondSuccess).IsTrue();
            await Assert.That(firstTable.Get(0).AsFuncRef()).IsSameReferenceAs(first.Functions[0]);
            await Assert
                .That(secondTable.Get(0).AsFuncRef())
                .IsSameReferenceAs(second.Functions[0]);
            await Assert.That(ReferenceEquals(first.Functions[0], second.Functions[0])).IsFalse();
            await Assert
                .That(((DefinedFunction)firstTable.Get(0).AsFuncRef()!).Instance)
                .IsSameReferenceAs(first);
            await Assert
                .That(((DefinedFunction)secondTable.Get(0).AsFuncRef()!).Instance)
                .IsSameReferenceAs(second);
        }
    }

    [Test]
    public async Task Import済みglobalを渡す_定義globalを評価してinstanceごとに構築する()
    {
        // Arrange
        var imported = new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(17));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0])),
                    HostLinkingModuleBinary.Globals(
                        (0x7F, true, [0x23, 0, 0x0B]),
                        (0x7F, false, [0x41, 42, 0x0B])
                    ),
                    HostLinkingModuleBinary.Exports(
                        ("imported", 3, 0),
                        ("copied", 3, 1),
                        ("constant", 3, 2)
                    )
                )
            )
            .Validate();

        // Act
        var first = new WasmInstance(module, WasmExecutionOptions.Default, [], [imported], [], []);
        var second = new WasmInstance(module, WasmExecutionOptions.Default, [], [imported], [], []);

        // Assert
        await Assert.That(first.Globals.Length).IsEqualTo(3);
        await Assert.That(second.Globals.Length).IsEqualTo(3);
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobalResource("imported")).IsSameReferenceAs(imported);
            await Assert.That(second.GetGlobalResource("imported")).IsSameReferenceAs(imported);
            await Assert.That(first.GetGlobal("copied").AsI32()).IsEqualTo(17);
            await Assert.That(first.GetGlobal("constant").AsI32()).IsEqualTo(42);
            await Assert
                .That(
                    ReferenceEquals(
                        first.GetGlobalResource("copied"),
                        second.GetGlobalResource("copied")
                    )
                )
                .IsFalse();
        }
    }
}
