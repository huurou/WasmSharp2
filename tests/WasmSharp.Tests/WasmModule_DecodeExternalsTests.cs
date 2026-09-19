using WasmSharp.Exceptions;
using WasmSharp.Modules.Imports;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 外部要素と巨大な未検証limits_両入力で実体を割り当てず静的定義を保持する(
        bool useStream
    )
    {
        // Arrange
        var types = HostLinkingModuleBinary.Types(([], [0x7F]));
        var imports = HostLinkingModuleBinary.Imports(
            ("env", "g", 3, [0x7F, 0]),
            ("env", "f", 0, [0]),
            ("env", "m", 2, HostLinkingModuleBinary.Limits(1, 2)),
            ("env", "t", 1, [0x70, 0, 1])
        );
        var functions = HostLinkingModuleBinary.Functions(0);
        var tables = HostLinkingModuleBinary.Tables((0x6F, uint.MaxValue, 0), (0x70, 0, null));
        var memories = HostLinkingModuleBinary.Memories((uint.MaxValue, 0), (0, null));
        var exports = HostLinkingModuleBinary.Exports(
            ("f", 0, 1),
            ("m", 2, uint.MaxValue),
            ("g", 3, 0),
            ("t", 1, 2)
        );
        var bytes = HostLinkingModuleBinary.Create(
            types,
            imports,
            functions,
            tables,
            memories,
            exports,
            HostLinkingModuleBinary.Code(([], [0x41, 42, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        Array.Clear(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Imports.Length).IsEqualTo(4);
            await Assert.That(module.Imports[0].Kind).IsEqualTo(WasmExternalKind.Global);
            await Assert.That(module.Imports[0].ByteOffset).IsEqualTo(8L + types.Length + 3);
            await Assert.That(((FunctionImport)module.Imports[1]).TypeIndex).IsEqualTo(0u);
            await Assert
                .That(((MemoryImport)module.Imports[2]).Type.Limits)
                .IsEqualTo(new WasmLimits(1, 2));
            await Assert
                .That(((TableImport)module.Imports[3]).Type.ElementKind)
                .IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(module.Functions.Length).IsEqualTo(1);
            await Assert.That(module.Functions[0].TypeIndex).IsEqualTo(0u);
            await Assert.That(module.Functions[0].Instructions[0].Immediate.AsI32()).IsEqualTo(42);
            await Assert.That(module.Tables.Length).IsEqualTo(2);
            await Assert.That(module.Tables[0].ElementKind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(module.Tables[0].Limits).IsEqualTo(new WasmLimits(uint.MaxValue, 0));
            await Assert
                .That(module.Tables[0].ByteOffset)
                .IsEqualTo(8L + types.Length + imports.Length + functions.Length + 3);
            await Assert.That(module.Tables[1].Limits).IsEqualTo(new WasmLimits(0));
            await Assert.That(module.Memories.Length).IsEqualTo(2);
            await Assert
                .That(module.Memories[0].Limits)
                .IsEqualTo(new WasmLimits(uint.MaxValue, 0));
            await Assert
                .That(module.Memories[0].ByteOffset)
                .IsEqualTo(
                    8L + types.Length + imports.Length + functions.Length + tables.Length + 3
                );
            await Assert.That(module.Exports.Length).IsEqualTo(4);
            await Assert.That(module.Exports[0].Kind).IsEqualTo(WasmExternalKind.Function);
            await Assert.That(module.Exports[0].Index).IsEqualTo(1u);
            await Assert.That(module.Exports[1].Kind).IsEqualTo(WasmExternalKind.Memory);
            await Assert.That(module.Exports[1].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Exports[2].Kind).IsEqualTo(WasmExternalKind.Global);
            await Assert.That(module.Exports[3].Kind).IsEqualTo(WasmExternalKind.Table);
            await Assert.That(module.Exports[3].Index).IsEqualTo(2u);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(stream.CanRead).IsTrue();
        }
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments((byte)2, "01000004", 13L)]
    [Arguments((byte)2, "0101FF000000", 12L)]
    [Arguments((byte)2, "0100000080", 15L)]
    [Arguments((byte)2, "0100000202", 14L)]
    [Arguments((byte)2, "010000037F02", 15L)]
    [Arguments((byte)4, "017F0000", 11L)]
    [Arguments((byte)4, "01700200", 12L)]
    [Arguments((byte)4, "01700100", 14L)]
    [Arguments((byte)5, "0102", 11L)]
    [Arguments((byte)5, "0100FFFFFFFF10", 16L)]
    [Arguments((byte)5, "010100", 13L)]
    [Arguments((byte)7, "010002", 13L)]
    [Arguments((byte)7, "01000380", 14L)]
    public async Task 外部要素の構文が不正_両入力で元位置付きDecode失敗になる(
        byte id,
        string hex,
        long offset
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(id, Convert.FromHexString(hex))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, id));
        }
    }

    [Test]
    public async Task Import関数の後の定義関数が破損_診断はmodule全体の関数添字を示す()
    {
        // Arrange
        var types = HostLinkingModuleBinary.Types(([], [0x7F]));
        var imports = HostLinkingModuleBinary.Imports(
            ("m", "f", 0, [0]),
            ("m", "g", 3, [0x7F, 0]),
            ("m", "f", 0, [0])
        );
        var functions = HostLinkingModuleBinary.Functions(0);
        var bytes = HostLinkingModuleBinary.Create(
            types,
            imports,
            functions,
            HostLinkingModuleBinary.Code(([], [0xFF]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - 1, 2, 10));
    }

    [Test]
    [Arguments((byte)2, "0100000000")]
    [Arguments((byte)7, "01000300")]
    public async Task 読取済みの型やexport参照が不正_検証済みにせずInstantiateを拒否する(
        byte id,
        string hex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(id, Convert.FromHexString(hex))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }
}
