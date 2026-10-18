using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments((byte)0x70, WasmValueKind.FuncRef)]
    [Arguments((byte)0x6F, WasmValueKind.ExternRef)]
    public async Task 参照nullと追加localを返す_参照型を保ちnull判定が1になる(
        byte kind,
        WasmValueKind expectedKind
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [kind, kind, kind, 0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Globals((kind, false, [0xD0, kind, 0x0B])),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    ([(1, kind)], [0xD0, kind, 0x20, 0, 0x23, 0, 0x20, 0, 0xD1, 0x0B])
                )
            )
        );

        // Act
        var values = module.Validate().Instantiate([]).GetFunction("run").Invoke([]).Values;

        // Assert
        var expectedNull =
            expectedKind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);
        await Assert
            .That(
                values.SequenceEqual([
                    expectedNull,
                    expectedNull,
                    expectedNull,
                    WasmValue.FromI32(1),
                ])
            )
            .IsTrue();
    }

    [Test]
    public async Task Globalと命令で同じ関数を参照する_所属instanceごとの関数同一性と非null判定を保つ()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x70, 0x70, 0x7F])),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Globals((0x70, false, [0xD2, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("target", 0, 0), ("run", 0, 1), ("g", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x0B]),
                        ([], [0xD2, 0, 0x23, 0, 0xD2, 0, 0xD1, 0x0B])
                    )
                )
            )
            .Validate();

        // Act
        var first = module.Instantiate([]);
        var second = module.Instantiate([]);
        var firstValues = first.GetFunction("run").Invoke([]).Values;
        var secondValues = second.GetFunction("run").Invoke([]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    firstValues.SequenceEqual([
                        WasmValue.FromFuncRef(first.GetFunction("target")),
                        first.GetGlobal("g"),
                        WasmValue.FromI32(0),
                    ])
                )
                .IsTrue();
            await Assert
                .That(
                    secondValues.SequenceEqual([
                        WasmValue.FromFuncRef(second.GetFunction("target")),
                        second.GetGlobal("g"),
                        WasmValue.FromI32(0),
                    ])
                )
                .IsTrue();
            await Assert
                .That(firstValues[0].AsFuncRef())
                .IsSameReferenceAs(firstValues[1].AsFuncRef());
            await Assert
                .That(ReferenceEquals(firstValues[0].AsFuncRef(), secondValues[0].AsFuncRef()))
                .IsFalse();
        }
    }

    [Test]
    [Arguments((byte)0x70, 0)]
    [Arguments((byte)0x70, 1)]
    [Arguments((byte)0x6F, 0)]
    [Arguments((byte)0x6F, 1)]
    public async Task 参照を引数とlocalとtyped_selectとifで渡す_複数結果の順序と選択した参照を保つ(
        byte kind,
        int condition
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([kind, kind, 0x7F], [kind, kind, 0x7F])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, kind)],
                            [
                                0x20,
                                0,
                                0x22,
                                3,
                                0x20,
                                1,
                                0x20,
                                2,
                                0x1C,
                                1,
                                kind,
                                0x20,
                                2,
                                0x04,
                                kind,
                                0x20,
                                3,
                                0x05,
                                0x20,
                                1,
                                0x0B,
                                0x20,
                                0,
                                0xD1,
                                0x0B,
                            ]
                        )
                    )
                )
            )
            .Validate();
        var first =
            kind == 0x70
                ? WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([])))
                : WasmValue.FromExternRef(new object());
        var second = kind == 0x70 ? WasmValue.FromFuncRef(null) : WasmValue.FromExternRef(null);
        var selected = condition != 0 ? first : second;

        // Act
        var values = module
            .Instantiate([])
            .GetFunction("run")
            .Invoke([first, second, WasmValue.FromI32(condition)])
            .Values;

        // Assert
        await Assert
            .That(values.SequenceEqual([selected, selected, WasmValue.FromI32(0)]))
            .IsTrue();
    }
}
