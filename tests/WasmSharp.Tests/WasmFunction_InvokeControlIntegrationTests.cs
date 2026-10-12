using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(1, 0, false, 11)]
    [Arguments(0, 0, false, 14)]
    [Arguments(1, 1, false, 10)]
    [Arguments(0, -1, false, 13)]
    [Arguments(1, int.MaxValue, false, 10)]
    [Arguments(0, -1, true, 13)]
    public async Task Loop引数とifとselectと分岐表を組み合わせる_深さ1で反復し外側値と結果を保持する(
        int condition,
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
                HostLinkingModuleBinary.Types(([0x7F, 0x7F, 0x7F], [0x7F, 0x7F]), ([0x7F], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            // 外側の値を残したまま、loop引数を1万回引き継ぐ。
                            0x41,
                            42,
                            0x20,
                            0,
                            0x03,
                            1,
                            0x01,
                            0x41,
                            1,
                            0x6B,
                            0x22,
                            0,
                            0x20,
                            0,
                            0x0D,
                            0,
                            0x0B,
                            0x1A,
                            // selectの結果をifの引数として渡し、選択側だけで加算する。
                            0x02,
                            0x7F,
                            0x02,
                            0x7F,
                            0x41,
                            7,
                            0x41,
                            9,
                            0x20,
                            1,
                            0x1B,
                            0x20,
                            1,
                            0x04,
                            1,
                            0x41,
                            1,
                            0x6A,
                            0x05,
                            0x41,
                            2,
                            0x6A,
                            0x0B,
                            // 内側または外側blockへ分岐し、飛び越えた加算を実行しない。
                            0x20,
                            2,
                            0x0E,
                            .. candidates,
                            1,
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
        var function = module.Validate().Instantiate([], new(1)).GetFunction("run");
        WasmValue[] arguments =
        [
            WasmValue.FromI32(10000),
            WasmValue.FromI32(condition),
            WasmValue.FromI32(selector),
        ];

        // Act
        var first = function.Invoke(arguments);
        var second = function.Invoke(arguments);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    first.Values.SequenceEqual([WasmValue.FromI32(42), WasmValue.FromI32(expected)])
                )
                .IsTrue();
            await Assert.That(second.Values.SequenceEqual(first.Values)).IsTrue();
        }
    }
}
