using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(0, false, 10)]
    [Arguments(1, false, 9)]
    [Arguments(2, false, 7)]
    [Arguments(-1, false, 7)]
    [Arguments(0, true, 7)]
    public async Task 分岐表の選択値が候補内または範囲外_保存した順序と空候補の既定へ分岐する(
        int selector,
        bool emptyCandidates,
        int expected
    )
    {
        // Arrange
        var candidates = emptyCandidates
            ? HostLinkingModuleBinary.LabelVector()
            : HostLinkingModuleBinary.LabelVector(0, 1);
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            0x02,
                            0x7F,
                            0x02,
                            0x7F,
                            0x41,
                            7,
                            0x20,
                            0,
                            0x0E,
                            .. candidates,
                            2,
                            0x0B,
                            0x41,
                            1,
                            0x6A,
                            0x0B,
                            0x41,
                            2,
                            0x6A,
                            0x0B,
                        ]
                    )
                )
            )
        );

        // Act
        var result = module
            .Validate()
            .Instantiate([])
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(selector)]);

        // Assert
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(expected);
    }

    [Test]
    public async Task 分岐表が複数値を持つ内側制御を終了する_外側operandと結果の宣言順を保持する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F, 0x7F, 0x7E]), ([], [0x7F, 0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    ([], Convert.FromHexString("412A02014107420941000E0100000B0B"))
                )
            )
        );

        // Act
        var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

        // Assert
        await Assert
            .That(
                result.Values.SequenceEqual([
                    WasmValue.FromI32(42),
                    WasmValue.FromI32(7),
                    WasmValue.FromI64(9),
                ])
            )
            .IsTrue();
    }
}
