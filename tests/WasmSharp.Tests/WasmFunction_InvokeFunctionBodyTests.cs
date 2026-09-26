using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 全値型の引数とlocalsを扱う_ビット列と参照と型別初期値を保つ(
        bool zeroInitialized
    )
    {
        // Arrange
        byte[] kinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F];
        var reference = new object();
        var functionReference = WasmFunction.CreateHost(new([], []), _ => new([]));
        WasmValue[] values =
        [
            WasmValue.FromI32(-1),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFFC12345),
            WasmValue.FromF64Bits(0xFFF8123456789ABC),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            WasmValue.FromFuncRef(functionReference),
            WasmValue.FromExternRef(reference),
        ];
        WasmValue[] zeros =
        [
            WasmValue.FromI32(0),
            WasmValue.FromI64(0),
            WasmValue.FromF32Bits(0),
            WasmValue.FromF64Bits(0),
            WasmValue.FromV128(0, 0),
            WasmValue.FromFuncRef(null),
            WasmValue.FromExternRef(null),
        ];
        List<byte> body = [];
        if (!zeroInitialized)
        {
            for (byte index = 0; index < 7; index++)
            {
                body.AddRange([0x20, index, 0x21, (byte)(index + 7)]);
            }
        }
        for (byte index = 0; index < 7; index++)
        {
            body.AddRange([0x20, (byte)(index + 7)]);
        }
        body.Add(0x0B);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((kinds, kinds)),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code((kinds.Select(x => (1u, x)).ToArray(), [.. body]))
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var result = WasmModule
            .Decode(stream)
            .Validate()
            .Instantiate([])
            .GetFunction("run")
            .Invoke(values);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(result.Values.SequenceEqual(zeroInitialized ? zeros : values))
                .IsTrue();
            await Assert
                .That(result.Values[5].AsFuncRef())
                .IsSameReferenceAs(zeroInitialized ? null : functionReference);
            await Assert
                .That(result.Values[6].AsExternRef())
                .IsSameReferenceAs(zeroInitialized ? null : reference);
        }
    }

    [Test]
    public async Task 定義関数を直接callする_global更新とreturnの複数結果を反映する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7E], [0x7E, 0x7F])),
            HostLinkingModuleBinary.Functions(0, 0),
            HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B])),
            HostLinkingModuleBinary.Exports(("run", 0, 0), ("g", 3, 0)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x20, 1, 0x10, 1, 0x0B]),
                ([], [0x20, 0, 0x24, 0, 0x20, 1, 0x23, 0, 0x0F, 0x00, 0x0B])
            )
        );

        // Act
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);
        var result = instance
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(42), WasmValue.FromI64(73)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.Length).IsEqualTo(2);
            await Assert.That(result.Values[0].AsI64()).IsEqualTo(73);
            await Assert.That(result.Values[1].AsI32()).IsEqualTo(42);
            await Assert.That(instance.GetGlobal("g").AsI32()).IsEqualTo(42);
        }
    }

    [Test]
    public async Task 引数をlocalへ設定して複数結果を返す_値と順序を保ち呼出しごとに分離する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F, 0x7E], [0x7F, 0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(
                    ([(1, 0x7F)], [0x20, 0, 0x22, 2, 0x1A, 0x20, 2, 0x20, 1, 0x0B])
                )
            )
        );

        // Act
        var function = module.Validate().Instantiate([]).GetFunction("run");
        var first = function.Invoke([WasmValue.FromI32(42), WasmValue.FromI64(73)]);
        var second = function.Invoke([WasmValue.FromI32(-1), WasmValue.FromI64(-2)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.Length).IsEqualTo(2);
            await Assert.That(first.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(first.Values[1].AsI64()).IsEqualTo(73);
            await Assert.That(second.Values[0].AsI32()).IsEqualTo(-1);
            await Assert.That(second.Values[1].AsI64()).IsEqualTo(-2);
        }
    }
}
