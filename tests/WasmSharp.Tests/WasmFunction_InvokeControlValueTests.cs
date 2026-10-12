using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(0, false, false)]
    [Arguments(0, false, true)]
    [Arguments(1, false, false)]
    [Arguments(1, false, true)]
    [Arguments(7, false, false)]
    [Arguments(7, false, true)]
    [Arguments(7, true, false)]
    [Arguments(7, true, true)]
    public async Task 制御値を直接callと内側分岐またはreturnで運ぶ_個数と順序と外側値と保存済み結果を保つ(
        int count,
        bool nullReferences,
        bool returnFromInnerBlock
    )
    {
        // Arrange
        byte[] allKinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F];
        var functionReference = nullReferences
            ? null
            : WasmFunction.CreateHost(new([], []), _ => new([]));
        var reference = nullReferences ? null : new object();
        var otherFunctionReference = WasmFunction.CreateHost(new([], []), _ => new([]));
        var otherReference = new object();
        WasmValue[] allValues =
        [
            WasmValue.FromI32(-42),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFF812345),
            WasmValue.FromF64Bits(0xFFF0123456789ABC),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            WasmValue.FromFuncRef(functionReference),
            WasmValue.FromExternRef(reference),
        ];
        WasmValue[] allOtherValues =
        [
            WasmValue.FromI32(73),
            WasmValue.FromI64(long.MaxValue),
            WasmValue.FromF32Bits(0x7FC54321),
            WasmValue.FromF64Bits(0x7FF8FEDCBA987654),
            WasmValue.FromV128(0xFEDCBA9876543210, 0x0123456789ABCDEF),
            WasmValue.FromFuncRef(otherFunctionReference),
            WasmValue.FromExternRef(otherReference),
        ];
        var kinds = allKinds[..count];
        var values = allValues[..count];
        var otherValues = allOtherValues[..count];
        List<byte> caller = [];
        List<byte> callee = [];
        for (byte index = 0; index < count; index++)
        {
            // 呼出し元のoperandを残し、呼出し先では引数を別のlocalsへコピーする。
            caller.AddRange([0x20, index]);
            callee.AddRange([0x20, index, 0x21, (byte)(count + index)]);
        }
        // 数値演算で選んだif側から呼び出す。誤った側ではtrapする。
        caller.AddRange([0x41, 3, 0x41, 4, 0x6A, 0x41, 7, 0x46, 0x04, 2]);
        for (byte index = 0; index < count; index++)
        {
            caller.AddRange([0x20, index]);
            callee.AddRange([0x20, (byte)(count + index)]);
        }
        caller.AddRange([0x10, 1, 0x05, 0x00, 0x0B, 0x0B]);
        // 全値型をblockの引数に渡し、内側からの退出で一時値と元の引数を破棄する。
        callee.AddRange([0x02, 0, 0x02, 0x40, 0x41, 55]);
        for (byte index = 0; index < count; index++)
        {
            callee.AddRange([0x20, (byte)(count + index)]);
        }
        callee.AddRange(
            returnFromInnerBlock ? [0x0F] : [0x41, 6, 0x41, 7, 0x6C, 0x41, 42, 0x46, 0x0D, 1]
        );
        callee.AddRange([0x00, 0x0B, 0x0B, 0x0F, 0x00, 0x0B]);
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        (kinds, kinds),
                        (kinds, [.. kinds, .. kinds]),
                        ([], kinds)
                    ),
                    HostLinkingModuleBinary.Functions(1, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [.. caller]),
                        (kinds.Select(x => (1u, x)).ToArray(), [.. callee])
                    )
                )
            )
            .Validate()
            .Instantiate([])
            .GetFunction("run");

        // Act
        var first = function.Invoke(values);
        var second = function.Invoke(otherValues);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.SequenceEqual([.. values, .. values])).IsTrue();
            await Assert
                .That(second.Values.SequenceEqual([.. otherValues, .. otherValues]))
                .IsTrue();
            if (count == 7)
            {
                for (var offset = 0; offset < 14; offset += 7)
                {
                    await Assert.That(first.Values[offset + 2].AsF32Bits()).IsEqualTo(0xFF812345u);
                    await Assert
                        .That(first.Values[offset + 3].AsF64Bits())
                        .IsEqualTo(0xFFF0123456789ABCul);
                    await Assert
                        .That(first.Values[offset + 4].AsV128())
                        .IsEqualTo((0x0123456789ABCDEFul, 0xFEDCBA9876543210ul));
                    await Assert
                        .That(first.Values[offset + 5].AsFuncRef())
                        .IsSameReferenceAs(functionReference);
                    await Assert
                        .That(first.Values[offset + 6].AsExternRef())
                        .IsSameReferenceAs(reference);
                    await Assert
                        .That(second.Values[offset + 5].AsFuncRef())
                        .IsSameReferenceAs(otherFunctionReference);
                    await Assert
                        .That(second.Values[offset + 6].AsExternRef())
                        .IsSameReferenceAs(otherReference);
                }
            }
        }
    }

    [Test]
    [Arguments(5, 6, 8)]
    [Arguments(10, 11, 13)]
    public async Task 制御値を同じ呼出し元から連続callする_内側分岐後も外側値とlocalsを保ち呼出し先localsを初期化する(
        int argument,
        int expectedFirst,
        int expectedSecond
    )
    {
        // Arrange
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7F], [0x7F, 0x7F]),
                        ([0x7F], [0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F])
                    ),
                    HostLinkingModuleBinary.Functions(1, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F)],
                            [
                                // 呼出し元のlocalとoperandを置き、先のcallの結果も残す。
                                0x41,
                                42,
                                0x22,
                                1,
                                0x1A,
                                0x41,
                                37,
                                0x20,
                                0,
                                0x10,
                                1,
                                0x20,
                                0,
                                0x41,
                                2,
                                0x6A,
                                0x10,
                                1,
                                0x20,
                                1,
                                0x0B,
                            ]
                        ),
                        (
                            [(1, 0x7F)],
                            [
                                // 初期localを外側operandへ残し、分岐で一時値55だけを破棄する。
                                0x20,
                                1,
                                0x02,
                                0x7F,
                                0x41,
                                55,
                                0x02,
                                0x40,
                                0x20,
                                0,
                                0x41,
                                1,
                                0x6A,
                                0x22,
                                1,
                                0x0C,
                                1,
                                0x00,
                                0x0B,
                                0x0B,
                                0x0F,
                                0x00,
                                0x0B,
                            ]
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([])
            .GetFunction("run");

        // Act
        var result = function.Invoke([WasmValue.FromI32(argument)]);

        // Assert
        await Assert
            .That(
                result
                    .Values.Select(x => x.AsI32())
                    .SequenceEqual([37, 0, expectedFirst, 0, expectedSecond, 42])
            )
            .IsTrue();
    }
}
