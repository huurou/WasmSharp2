using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments((byte)0x6E, WasmValueKind.AnyRef, false)]
    [Arguments((byte)0x6D, WasmValueKind.EqRef, false)]
    [Arguments((byte)0x6C, WasmValueKind.I31Ref, false)]
    [Arguments((byte)0x6B, WasmValueKind.StructRef, false)]
    [Arguments((byte)0x6A, WasmValueKind.ArrayRef, false)]
    [Arguments((byte)0x71, WasmValueKind.AnyRef, true)]
    public async Task GC参照型を宣言する_参照命令とlocalとglobalとテーブルのnullを引数に渡せる(
        byte kind,
        WasmValueKind expected,
        bool bottom
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [kind, kind, kind, kind]), ([kind], [0x7F])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Tables((kind, 1, null)),
            HostLinkingModuleBinary.Globals((kind, false, [0xD0, 0x71, 0x0B])),
            HostLinkingModuleBinary.Exports(("values", 0, 0), ("is_null", 0, 1)),
            HostLinkingModuleBinary.Code(
                ([(1, kind)], [0xD0, kind, 0x20, 0, 0x23, 0, 0x41, 0, 0x25, 0, 0x0B]),
                ([], [0x20, 0, 0xD1, 0x0B])
            )
        );
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var isNull = instance.GetFunction("is_null");

        // Act
        var values = instance.GetFunction("values").Invoke(context, []).Values;
        var flags = values.Select(x => isNull.Invoke(context, [x]).Values[0].AsI32()).ToArray();

        // Assert
        using (Assert.Multiple())
        {
            var parameter = isNull.Type.ParameterTypes[0];
            await Assert.That(parameter.Kind).IsEqualTo(expected);
            await Assert.That(parameter.IsNullable).IsTrue();
            await Assert.That(parameter.IsBottomHeapType).IsEqualTo(bottom);
            await Assert.That(flags).IsEquivalentTo([1, 1, 1, 1]);
        }
    }

    [Test]
    public async Task ホストが非nullのeqとして返したi31を関数の引数に渡す_非nullのanyとして同じi31を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var host = WasmFunction.CreateHost(
            WasmFunctionType.Create([], [WasmValueType.Reference(WasmValueKind.EqRef, false)]),
            (_, _) => new WasmResults([WasmValue.FromI31(0)])
        );
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x64, 0x6D, 1, 0x64, 0x6E], [0x20, 0])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var value = function.Invoke(context, host.Invoke(context, []).Values.AsSpan()).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value.Kind).IsEqualTo(WasmValueKind.I31Ref);
            await Assert.That(value.IsNullReference).IsFalse();
            await Assert.That(value.AsI31Unsigned()).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments((byte)0x1D)] // i31.get_s
    [Arguments((byte)0x1E)] // i31.get_u
    public async Task 符号付きまたは符号なしの取り出しにnullを渡す_i31参照のtrapを報告する(byte opcode)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x6C, 1, 0x7F], [0x20, 0, 0xFB, opcode])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act & Assert
        var error = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.I31Ref)]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith("null i31 reference");
            await Assert.That(error.Reason).IsEqualTo(WasmTrapReason.NullI31Reference);
        }
    }

    [Test]
    [Arguments(0, 0, 0)]
    [Arguments(int.MinValue, 0, 0)]
    [Arguments(0x3FFF_FFFF, 0x3FFF_FFFF, 0x3FFF_FFFF)]
    [Arguments(0x4000_0000, -0x4000_0000, 0x4000_0000)]
    [Arguments(-1, -1, 0x7FFF_FFFF)]
    public async Task 生成したi31を符号付きと符号なしで取り出す_下位31ビットをそれぞれ拡張する(
        int input,
        int signed,
        int unsigned
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x7F, 2, 0x7F, 0x7F], [0x20, 0, 0xFB, 0x1C, 0xFB, 0x1D, 0x20, 0, 0xFB, 0x1C, 0xFB, 0x1E])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var values = function.Invoke(context, [WasmValue.FromI32(input)]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].AsI32()).IsEqualTo(signed);
            await Assert.That(values[1].AsI32()).IsEqualTo(unsigned);
        }
    }
}
