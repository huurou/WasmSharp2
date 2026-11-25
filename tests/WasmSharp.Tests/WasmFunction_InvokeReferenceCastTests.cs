using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("02646C2000FB1801006E6CD10F0BFB1D")] // br_on_castの分岐先でi31を読む。
    [Arguments("026E2000FB1901006E6C2201FB1D0F0BD1")] // br_on_cast_failの継続側で非nullのi31をlocalに渡す。
    public async Task 非nullのi31へのキャストで分岐する_成功側でi31の値を返し失敗側でnullを判定する(string body)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x6E, 1, 0x7F], Convert.FromHexString(body), locals: [1, 1, 0x64, 0x6C])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var nonNull = function.Invoke(context, [WasmValue.FromI31(42)]).Values[0].AsI32();
        var nil = function.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.AnyRef)]).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nonNull).IsEqualTo(42);
            await Assert.That(nil).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("0263712000FB1803006E710F0B1A4107FB1C")] // br_on_castの継続側で非nullを返す。
    [Arguments("02646E2000FB1903006E711A4107FB1C0B")] // br_on_cast_failの分岐側で非nullを返す。
    public async Task 底型へのキャストでnullを許す_失敗側の参照を非null型の結果として返せる(string body)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x6E, 1, 0x64, 0x6E], Convert.FromHexString(body))
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var nonNull = function.Invoke(context, [WasmValue.FromI31(42)]).Values[0].AsI31Unsigned();
        var nil = function.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.AnyRef)]).Values[0].AsI31Unsigned();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nonNull).IsEqualTo(42);
            await Assert.That(nil).IsEqualTo(7);
        }
    }

    [Test]
    [Arguments((byte)0x18, (byte)1, (byte)0x6C, 7, 15)] // br_on_cast (ref i31)
    [Arguments((byte)0x19, (byte)1, (byte)0x6C, 15, 7)] // br_on_cast_fail (ref i31)
    [Arguments((byte)0x18, (byte)3, (byte)0x71, 15, 7)] // br_on_cast (ref null none)
    [Arguments((byte)0x19, (byte)3, (byte)0x71, 7, 15)] // br_on_cast_fail (ref null none)
    public async Task キャスト結果で外側のラベルに分岐する_両方の経路で先行する整数と元の参照を返す(
        byte opcode,
        byte flags,
        byte target,
        int expectedI31,
        int expectedNull
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create(
                [2, 0x60, 1, 0x6E, 2, 0x7F, 0x6E, 0x60, 0, 2, 0x7F, 0x6E],
                [
                    0x20,
                    0,
                    0xFB,
                    0x15,
                    0x71,
                    0x1A, // 一つ目の対象型を使うref.testの結果を捨てる。
                    0x02,
                    1,
                    0x02,
                    0x40, // 外側のラベルはi32、anyrefを受け取る。
                    0x42,
                    17,
                    0x41,
                    7,
                    0x20,
                    0, // ラベルに渡す2値の下に余分なi64を積む。
                    0xFB,
                    opcode,
                    flags,
                    1,
                    0x6E,
                    target,
                    0x21,
                    1,
                    0x41,
                    8,
                    0x6A,
                    0x20,
                    1,
                    0x0F, // 継続時には整数に8を足して同じ参照と返す。
                    0x0B,
                    0x00,
                    0x0B,
                ],
                locals: [1, 1, 0x6E]
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var nonNull = function.Invoke(context, [WasmValue.FromI31(42)]).Values;
        var nil = function.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.AnyRef)]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nonNull[0].AsI32()).IsEqualTo(expectedI31);
            await Assert.That(nonNull[1].AsI31Unsigned()).IsEqualTo(42);
            await Assert.That(nil[0].AsI32()).IsEqualTo(expectedNull);
            await Assert.That(nil[1].IsNullReference).IsTrue();
        }
    }

    [Test]
    [Arguments((byte)0x16, false)]
    [Arguments((byte)0x17, true)]
    public async Task 参照のキャスト先に関数型を指定する_署名の一致する関数をそのまま返し不一致ならtrapになる(
        byte opcode,
        bool nullable
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([2, 0x60, 0, 1, 0x7F, 0x60, 1, 0x70, 1, 0x63, 0], [0x20, 0, 0xFB, opcode, 0], typeIndex: 1)
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var match = WasmFunction.CreateHost(WasmFunctionType.Create([], [WasmValueKind.I32]), (_, _) =>
            new WasmResults([WasmValue.FromI32(9)])
        );
        var mismatch = WasmFunction.CreateHost(WasmFunctionType.Create([], []), (_, _) => new WasmResults([]));
        var nil = WasmValue.FromFuncRef(null);

        // Act
        var value = function.Invoke(context, [WasmValue.FromFuncRef(match)]).Values[0].AsFuncRef();

        // Act & Assert
        var error = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromFuncRef(mismatch)]))
            .ThrowsExactly<WasmTrapException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value).IsSameReferenceAs(match);
            await Assert.That(error!.Reason).IsEqualTo(WasmTrapReason.CastFailure);
            await Assert.That(error.Message).StartsWith("cast failure");
        }
        if (nullable)
        {
            await Assert.That(function.Invoke(context, [nil]).Values[0].IsNullReference).IsTrue();
        }
        else
        {
            // Act & Assert
            var nullError = await Assert.That(() => function.Invoke(context, [nil])).ThrowsExactly<WasmTrapException>();
            await Assert.That(nullError!.Message).StartsWith("cast failure");
        }
    }

    [Test]
    [Arguments((byte)0x14, (byte)0x6C, 1, 0)] // (ref i31)
    [Arguments((byte)0x15, (byte)0x6C, 1, 1)] // (ref null i31)
    [Arguments((byte)0x14, (byte)0x6B, 0, 0)] // (ref struct)
    [Arguments((byte)0x15, (byte)0x6B, 0, 1)] // (ref null struct)
    [Arguments((byte)0x14, (byte)0x71, 0, 0)] // (ref none)
    [Arguments((byte)0x15, (byte)0x71, 0, 1)] // (ref null none)
    public async Task 参照の型判定にi31型の引数を渡す_実行時の値と対象型のnull可否で判定する(
        byte opcode,
        byte heapType,
        int expectedI31,
        int expectedNull
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 1, 0x6C, 1, 0x7F], [0x20, 0, 0xFB, opcode, heapType])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var nonNull = function.Invoke(context, [WasmValue.FromI31(42)]).Values[0].AsI32();
        var nil = function.Invoke(context, [WasmValue.FromNullReference(WasmValueKind.I31Ref)]).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nonNull).IsEqualTo(expectedI31);
            await Assert.That(nil).IsEqualTo(expectedNull);
        }
    }

    [Test]
    [Arguments(-1, 0x7FFF_FFFF, 1)]
    [Arguments(0, 1, 0)]
    [Arguments(null, null, 1)]
    [Arguments(0, null, 0)]
    [Arguments(null, 0, 0)]
    public async Task 参照の等価判定でi31とnullを比較する_同じ参照値のときだけ1を返す(int? left, int? right, int expected)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var function = ReferenceControlModule
            .Create([1, 0x60, 2, 0x6D, 0x6D, 1, 0x7F], [0x20, 0, 0x20, 1, 0xD3])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var first = left.HasValue ? WasmValue.FromI31(left.Value) : WasmValue.FromNullReference(WasmValueKind.EqRef);
        var second = right.HasValue ? WasmValue.FromI31(right.Value) : WasmValue.FromNullReference(WasmValueKind.I31Ref);

        // Act
        var result = function.Invoke(context, [first, second]).Values[0].AsI32();

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
