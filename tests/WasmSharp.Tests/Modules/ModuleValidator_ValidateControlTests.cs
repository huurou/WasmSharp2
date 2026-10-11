using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(0x02, 0)]
    [Arguments(0x03, 0)]
    [Arguments(0x04, 0)]
    [Arguments(0x04, -1)]
    public async Task 制御の通常経路へ全値型を渡す_結果のbit列と参照と宣言順を保持する(
        int opcode,
        int condition
    )
    {
        // Arrange
        byte[] kinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F];
        var functionReference = WasmFunction.CreateHost(new([], []), _ => new([]));
        var externalReference = new object();
        WasmValue[] values =
        [
            WasmValue.FromI32(-1),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFFC12345),
            WasmValue.FromF64Bits(0xFFF8123456789ABC),
            WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            WasmValue.FromFuncRef(functionReference),
            WasmValue.FromExternRef(externalReference),
        ];
        List<byte> body = [];
        for (byte index = 0; index < kinds.Length; index++)
        {
            body.AddRange([0x20, index]);
        }
        if (opcode == 0x04)
        {
            body.AddRange([0x20, 7]);
        }
        body.AddRange([(byte)opcode, 1, 0x01]);
        if (opcode == 0x04)
        {
            body.AddRange([0x05, 0x01]);
        }
        body.AddRange([0x0B, 0x0B]);
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([.. kinds, 0x7F], kinds), (kinds, kinds)),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], [.. body]))
            )
        );

        // Act
        var validated = module.Validate();
        var result = validated
            .Instantiate([])
            .GetFunction("run")
            .Invoke([.. values, WasmValue.FromI32(condition)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(validated).IsSameReferenceAs(module);
            await Assert.That(result.Values.SequenceEqual(values)).IsTrue();
            await Assert.That(result.Values[5].AsFuncRef()).IsSameReferenceAs(functionReference);
            await Assert.That(result.Values[6].AsExternRef()).IsSameReferenceAs(externalReference);
        }
    }

    [Test]
    [Arguments("0240010B0340010B41090B", 9)]
    [Arguments("4100047F41070541080B0B", 8)]
    [Arguments("4101047F41070541080B0B", 7)]
    [Arguments("41094100040141006A0B0B", 9)]
    [Arguments("410004400B41090B", 9)]
    [Arguments("4101044005010B41090B", 9)]
    [Arguments("41094101040141016A0B0B", 10)]
    public async Task 通常終了とifの選択側を実行_後続へ結果を渡す(string body, int expected)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7F]), ([0x7F], [0x7F])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act
        var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

        // Assert
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0x02, 0, 7L)]
    [Arguments(0x03, 0, 7L)]
    [Arguments(0x04, 0, 8L)]
    [Arguments(0x04, 1, 7L)]
    public async Task 入口引数と結果の型が異なる_選択経路の結果へ置き換えて通常終了する(
        int opcode,
        int condition,
        long expected
    )
    {
        // Arrange
        List<byte> body = [0x41, 9];
        if (opcode == 0x04)
        {
            body.AddRange([0x41, (byte)condition]);
        }
        body.AddRange([(byte)opcode, 1, 0x1A, 0x42, 7]);
        if (opcode == 0x04)
        {
            body.AddRange([0x05, 0x1A, 0x42, 8]);
        }
        body.AddRange([0x0B, 0x0B]);
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [0x7E]), ([0x7F], [0x7E])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Exports(("run", 0, 0)),
                HostLinkingModuleBinary.Code(([], [.. body]))
            )
        );

        // Act
        var result = module.Validate().Instantiate([]).GetFunction("run").Invoke([]);

        // Assert
        await Assert.That(result.Values[0].AsI64()).IsEqualTo(expected);
    }
}
