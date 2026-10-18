using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(
        "0C",
        "0123456789ABCDEFFEDCBA9876543210",
        0xEFCDAB8967452301UL,
        0x1032547698BADCFEUL
    )]
    [Arguments(
        "8C00",
        "4523C17F0000000000000080230180FF",
        0x000000007FC12345UL,
        0xFF80012380000000UL
    )]
    [Arguments(
        "8C80808000",
        "BC9A78563412F87F0000000000000080",
        0x7FF8123456789ABCUL,
        0x8000000000000000UL
    )]
    public async Task V128定数の最短か許容非最短番号がある_両入力の4段階で全bitを保持する(
        string opcode,
        string bits,
        ulong low,
        ulong high
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString("FD" + opcode + bits + "0B")))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        Array.Clear(bytes);
        foreach (var module in modules)
        {
            module.Validate();
            var result = module.Instantiate([]).GetFunction("run").Invoke([]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert
                    .That(module.Functions[0].Instructions[0].Immediate.AsV128())
                    .IsEqualTo((low, high));
                await Assert.That(result.Values.Length).IsEqualTo(1);
                await Assert.That(result.Values[0].AsV128()).IsEqualTo((low, high));
            }
        }
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, -1)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, int.MinValue)]
    public async Task V128のselectがある_I32条件で選択したNaNと符号付きゼロの全bitを保持する(
        bool typed,
        int condition
    )
    {
        // Arrange
        byte[] select = typed ? [0x1C, 1, 0x7B] : [0x1B];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7B, 0x7B, 0x7F], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x0B]))
        );
        var first = WasmValue.FromV128(0x800000007FC12345UL, 0xFF80012300000000UL);
        var second = WasmValue.FromV128(0xFFF8123456789ABCUL, 0x8000000000000000UL);
        var module = WasmModule.Decode(bytes);

        // Act
        var result = module
            .Validate()
            .Instantiate([])
            .GetFunction("run")
            .Invoke([first, second, WasmValue.FromI32(condition)]);

        // Assert
        await Assert
            .That(result.Values[0].AsV128())
            .IsEqualTo((condition != 0 ? first : second).AsV128());
    }
}
