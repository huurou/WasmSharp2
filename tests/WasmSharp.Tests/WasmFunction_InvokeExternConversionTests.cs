using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(-1, 0x7FFF_FFFF)]
    public async Task ホストへi31を外部参照として渡して戻す_元の31ビットと外部参照の同一性を保持する(
        int signed,
        int unsigned
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var (internalize, externalize) = CreateNonNullExternConversions(context);
        var externalType = WasmValueType.Reference(WasmValueKind.ExternRef, false);
        var host = WasmFunction.CreateHost(WasmFunctionType.Create([externalType], [externalType]), (_, args) =>
            new WasmResults([WasmValue.FromExternRef(args[0].AsExternRef())])
        );

        var original = WasmValue.FromI31(signed);
        var different = WasmValue.FromI31(signed ^ 1);

        // Act
        var external = externalize.Invoke(context, [original]).Values[0];
        var returned = host.Invoke(context, [external]).Values[0];
        var result = internalize.Invoke(context, [returned]).Values[0];
        var externalAgain = externalize.Invoke(context, [result]).Values[0];
        var values = new HashSet<WasmValue> { original, result, different };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(external.AsExternRef()).IsNotNull();
            await Assert.That(externalAgain.AsExternRef()).IsSameReferenceAs(returned.AsExternRef());
            await Assert.That(result.AsI31Signed()).IsEqualTo(signed);
            await Assert.That(result.AsI31Unsigned()).IsEqualTo(unsigned);
            await Assert.That(original.Equals(result)).IsTrue();
            await Assert.That(result.Equals(original)).IsTrue();
            await Assert.That(original == result).IsTrue();
            await Assert.That(result != different).IsTrue();
            await Assert.That(result.GetHashCode()).IsEqualTo(original.GetHashCode());
            await Assert.That(result.Equals(WasmValue.FromI32(unsigned))).IsFalse();
            await Assert.That(values.Count).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(7, 7, 1)]
    [Arguments(-1, int.MaxValue, 1)]
    [Arguments(7, 8, 0)]
    public async Task 個別に作ったi31を外部参照へ変換して戻す_元の31ビットに従って参照の同一性を判定する(
        int first,
        int second,
        int expected
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        // ref.i31、extern.convert_any、any.convert_extern、ref.cast eqの順に両方の引数を変換する。
        byte[] roundTrip = [0xFB, 0x1C, 0xFB, 0x1B, 0xFB, 0x1A, 0xFB, 0x16, 0x6D];
        var equal = ReferenceControlModule
            .Create([1, 0x60, 2, 0x7F, 0x7F, 1, 0x7F], [0x20, 0, .. roundTrip, 0x20, 1, .. roundTrip, 0xD3])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var result = equal.Invoke(context, [WasmValue.FromI32(first), WasmValue.FromI32(second)]);

        // Assert
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(expected);
    }

    [Test]
    public async Task 外部化したi31の参照先をホスト参照として渡す_外部参照を経由して同じオブジェクトが戻る()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var (internalize, externalize) = CreateNonNullExternConversions(context);
        var token = externalize.Invoke(context, [WasmValue.FromI31(7)]).Values[0].AsExternRef();
        var host = WasmValue.FromHostRef(token);

        // Act
        var external = externalize.Invoke(context, [host]).Values[0];
        var result = internalize.Invoke(context, [WasmValue.FromExternRef(external.AsExternRef())]).Values[0];

        // Assert
        await Assert.That(result.AsHostRef()).IsSameReferenceAs(token);
    }

    [Test]
    public async Task 同じ内容の異なる外部参照を内部参照に変換して戻す_それぞれ元のホスト参照を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var roundTrip = ReferenceControlModule
            .Create([1, 0x60, 1, 0x6F, 1, 0x6F], [0x20, 0, 0xFB, 0x1A, 0xFB, 0x1B])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var first = new string('a', 3);
        var second = new string('a', 3);

        // Act
        var firstResult = roundTrip.Invoke(context, [WasmValue.FromExternRef(first)]).Values[0];
        var secondResult = roundTrip.Invoke(context, [WasmValue.FromExternRef(second)]).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstResult.AsExternRef()).IsSameReferenceAs(first);
            await Assert.That(secondResult.AsExternRef()).IsSameReferenceAs(second);
        }
    }

    private static (WasmFunction Internalize, WasmFunction Externalize) CreateNonNullExternConversions(
        WasmExecutionContext context
    )
    {
        var externalize = ReferenceControlModule
            .Create([1, 0x60, 1, 0x64, 0x6E, 1, 0x64, 0x6F], [0x20, 0, 0xFB, 0x1B])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var internalize = ReferenceControlModule
            .Create([1, 0x60, 1, 0x64, 0x6F, 1, 0x64, 0x6E], [0x20, 0, 0xFB, 0x1A])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        return (internalize, externalize);
    }
}
