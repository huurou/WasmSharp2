using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("E001", "452381FF000000804523C1FF01000000", "", "4523817F000000004523C17F01000000")]
    [Arguments("ED01", "BC9A78563412F07F0000000000000080", "", "BC9A78563412F0FF0000000000000000")]
    [Arguments(
        "E801",
        "000000000000008000004040000080C0",
        "000000800000000000000040000040C0",
        "000000800000008000000040000080C0"
    )]
    [Arguments(
        "F501",
        "00000000000000000000000000000080",
        "00000000000000800000000000000000",
        "00000000000000000000000000000000"
    )]
    [Arguments(
        "EA01",
        "4523817F0000803F0000008000000000",
        "000000404365877F0000000000000080",
        "4523817F0000803F0000008000000000"
    )]
    [Arguments(
        "EA01",
        "000000404365877F0000000000000080",
        "4523817F0000803F0000008000000000",
        "000000404365877F0000000000000080"
    )]
    [Arguments(
        "F701",
        "BC9A78563412F07F0000000000000080",
        "00000000000000400000000000000000",
        "BC9A78563412F07F0000000000000080"
    )]
    [Arguments(
        "F701",
        "00000000000000400000000000000000",
        "BC9A78563412F07F0000000000000080",
        "00000000000000400000000000000000"
    )]
    [Arguments("6A", "0000003F0000C03F00002040000000BF", "", "00000000000000400000004000000080")]
    [Arguments("9401", "000000000000F8BF00000000000004C0", "", "00000000000000C000000000000000C0")]
    [Arguments("F801", "4523817F0000807F000080FF3333F3BF", "", "00000000FFFFFF7F00000080FFFFFFFF")]
    [Arguments("F901", "3333F3BF0000804F0000004F0000C07F", "", "00000000FFFFFFFF0000008000000000")]
    [Arguments("FC01", "000000000000E041000020000000E0C1", "", "FFFFFF7F000000800000000000000000")]
    [Arguments("FD01", "666666666666FEBF000000000000F041", "", "00000000FFFFFFFF0000000000000000")]
    [Arguments("FA01", "FFFFFFFF0000008001000001FFFFFF7F", "", "000080BF000000CF0000804B0000004F")]
    [Arguments("FB01", "FFFFFFFF000000800100000100000000", "", "0000804F0000004F0000804B00000000")]
    [Arguments("FE01", "FFFFFFFF000000804523817FFFFFFFFF", "", "000000000000F0BF000000000000E0C1")]
    [Arguments("FF01", "FFFFFFFF000000804523817FFFFFFFFF", "", "0000E0FFFFFFEF41000000000000E041")]
    [Arguments("5E", "000000100000F03F0000000000000080", "", "0000803F000000800000000000000000")]
    [Arguments("5F", "01000000000000804523817FFFFFFFFF", "", "000000000000A0360000000000000080")]
    public async Task SIMD浮動接続のbitと丸めと変換を実行する_両入力の公開4段階でlaneの結果を保持する(
        string opcode,
        string first,
        string second,
        string expected
    )
    {
        // Arrange
        byte[] parameters = second.Length == 0 ? [0x7B] : [0x7B, 0x7B];
        var pushes = parameters.SelectMany((_, x) => new byte[] { 0x20, (byte)x });
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameters, [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(
                ([], [.. pushes, 0xFD, .. Convert.FromHexString(opcode), 0x0B])
            )
        );
        WasmValue[] operands =
            second.Length == 0 ? [Vector(first)] : [Vector(first), Vector(second)];
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        foreach (var module in modules)
        {
            var actual = module
                .Validate()
                .Instantiate([])
                .GetFunction("run")
                .Invoke(operands)
                .Values;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(actual.Length).IsEqualTo(1);
                await Assert.That(actual[0].AsV128()).IsEqualTo(Vector(expected).AsV128());
            }
        }
    }
}
