using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("2000FB020000")] // struct.get
    [Arguments("2000FB030001")] // struct.get_s
    [Arguments("2000FB040001")] // struct.get_u
    [Arguments("20004109FB0500004100")] // struct.set
    public async Task 構造体のnull参照でフィールドにアクセスする_命令位置と原因を持つtrapを返す(string encodedBody)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x5F, 2, 0x7F, 1, 0x78, 1, 0x60, 1, 0x63, 0, 1, 0x7F]),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Exports(("f", 0, 0)),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(encodedBody), 0x0B]))
        );
        var function = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []).GetFunction("f");
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, Array.LastIndexOf(binary, (byte)0xFB), 0);
        var nil = WasmValue.FromNullReference(WasmValueKind.StructRef);

        // Act & Assert
        var error = await Assert.That(() => function.Invoke(context, [nil])).ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Reason).IsEqualTo(WasmTrapReason.NullStructReference);
            await Assert.That(error.Message).StartsWith("null structure reference");
            await Assert.That(error.Location).IsEqualTo(location);
        }
    }

    [Test]
    [Arguments((byte)0x78, -128, 128, -1, 255)]
    [Arguments((byte)0x77, -32640, 32896, 32767, 32767)]
    public async Task 詰め込みフィールドを生成して更新する_各時点の下位ビットを符号付きと符号なしで読める(
        byte packedType,
        int signedInitial,
        int unsignedInitial,
        int signedAfterSet,
        int unsignedAfterSet
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create(
                [2, 0x5F, 1, packedType, 1, 0x60, 2, 0x7F, 0x7F, 4, 0x7F, 0x7F, 0x7F, 0x7F],
                [
                    0x20,
                    0,
                    0xFB,
                    0,
                    0,
                    0x21,
                    2, // 最初の引数で構造体を生成する。
                    0x20,
                    2,
                    0xFB,
                    3,
                    0,
                    0,
                    0x20,
                    2,
                    0xFB,
                    4,
                    0,
                    0,
                    0x20,
                    2,
                    0x20,
                    1,
                    0xFB,
                    5,
                    0,
                    0, // 同じ構造体に次の引数を書き込む。
                    0x20,
                    2,
                    0xFB,
                    3,
                    0,
                    0,
                    0x20,
                    2,
                    0xFB,
                    4,
                    0,
                    0,
                ],
                typeIndex: 1,
                locals: [1, 1, 0x63, 0]
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var values = function.Invoke(context, [WasmValue.FromI32(0x1_8080), WasmValue.FromI32(0x3_7FFF)]).Values;

        // Assert
        var actual = (values[0].AsI32(), values[1].AsI32(), values[2].AsI32(), values[3].AsI32());
        await Assert.That(actual).IsEqualTo((signedInitial, unsignedInitial, signedAfterSet, unsignedAfterSet));
    }

    [Test]
    public async Task 数値とベクトルと参照の既定値で構造体を生成する_各型のゼロとnullを返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        byte[] fieldKinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F, 0x6B];
        var function = ReferenceControlModule
            .Create(
                [
                    2,
                    0x5F,
                    (byte)fieldKinds.Length,
                    .. fieldKinds.SelectMany(x => new byte[] { x, 0 }),
                    0x60,
                    0,
                    (byte)fieldKinds.Length,
                    .. fieldKinds,
                ],
                [
                    0xFB,
                    1,
                    0,
                    0x21,
                    0, // struct.new_defaultの結果をlocalに保存する。
                    .. Enumerable.Range(0, fieldKinds.Length).SelectMany(x => new byte[] { 0x20, 0, 0xFB, 2, 0, (byte)x }),
                ],
                typeIndex: 1,
                locals: [1, 1, 0x64, 0] // struct.new_defaultの結果を非nullのlocalに保存する。
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var values = function.Invoke(context, []).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].AsI32()).IsEqualTo(0);
            await Assert.That(values[1].AsI64()).IsEqualTo(0L);
            await Assert.That(values[2].AsF32Bits()).IsEqualTo(0u);
            await Assert.That(values[3].AsF64Bits()).IsEqualTo(0UL);
            await Assert.That(values[4].AsV128()).IsEqualTo((0UL, 0UL));
            await Assert.That(values[5].AsFuncRef()).IsNull();
            await Assert.That(values[6].AsExternRef()).IsNull();
            await Assert.That(values[7]).IsEqualTo(WasmValue.FromNullReference(WasmValueKind.StructRef));
        }
    }

    [Test]
    public async Task 異なる型の値から構造体を生成する_各フィールドから宣言順の値を取り出せる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create(
                [2, 0x5F, 2, 0x7E, 0, 0x7F, 0, 0x60, 0, 2, 0x7E, 0x7F],
                [
                    0x42,
                    37,
                    0x41,
                    19,
                    0xFB,
                    0,
                    0,
                    0x22,
                    0, // struct.newの結果をlocalにも保持する。
                    0xFB,
                    2,
                    0,
                    0,
                    0x20,
                    0,
                    0xFB,
                    2,
                    0,
                    1,
                ],
                typeIndex: 1,
                locals: [1, 1, 0x64, 0] // struct.newの結果を非nullのlocalに保存する。
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var values = function.Invoke(context, []).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].AsI64()).IsEqualTo(37L);
            await Assert.That(values[1].AsI32()).IsEqualTo(19);
        }
    }
}
