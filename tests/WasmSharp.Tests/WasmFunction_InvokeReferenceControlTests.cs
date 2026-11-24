using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("200021012001")] // local.set 1、local.get 1
    [Arguments("200022011A2001")] // local.tee 1、drop、local.get 1
    [Arguments("2000210102646F20010B")] // 外側で初期化し、block内で読む
    [Arguments("200021010240200021010B2001")] // 内側で再代入しても、外側の初期化は有効
    public async Task 非nullの引数でlocalを初期化して読み取る_同じ参照を返す(string instructions)
    {
        // Arrange
        var module = ReferenceControlModule.CreateWithNonNullableLocal(instructions);
        var context = new WasmExecutionContext();
        var value = new object();

        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act
        var result = function.Invoke(context, [WasmValue.FromExternRef(value)]).Values[0];

        // Assert
        await Assert.That(result.AsExternRef()).IsSameReferenceAs(value);
    }

    [Test]
    public async Task 具体的な関数参照をref_as_non_nullで絞り込む_関数の同一性を保ちnullでtrapになる()
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [2, 0x60, 0, 0, 0x60, 1, 0x63, 0, 1, 0x64, 0], // () -> () と (ref null 0) -> (ref 0)
            [0x20, 0, 0xD4],
            typeIndex: 1
        );
        var target = WasmFunction.CreateHost(new WasmFunctionType([], []), (_, _) => new WasmResults([]));
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act
        var result = function.Invoke(context, [WasmValue.FromFuncRef(target)]).Values[0];

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromFuncRef(null)]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(result.AsFuncRef()).IsSameReferenceAs(target);
            await Assert.That(exception!.Message).StartsWith("null reference");
            await Assert.That(exception.Reason).IsEqualTo(WasmTrapReason.NullReference);
            await Assert.That(exception.Location?.Stage).IsEqualTo(WasmProcessingStage.Invoke);
        }
    }

    [Test]
    public async Task 整数2個をbr_on_nullで渡す_nullなら分岐し非nullなら絞り込んだ参照を返す()
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [2, 0x60, 1, 0x6F, 3, 0x7F, 0x7E, 0x6F, 0x60, 0, 2, 0x7F, 0x7E],
            [
                0x02,
                1, // block (result i32 i64)
                0x41,
                9,
                0x41,
                3,
                0x42,
                4, // 最下段の9は分岐先へ渡さない
                0x20,
                0,
                0xD5,
                0, // br_on_null 0 (local.get 0)
                0x21,
                1, // 非nullの参照だけを受けるlocal
                0x41,
                7,
                0x42,
                8,
                0x20,
                1,
                0x0F, // return 7, 8, ref
                0x0B,
                0xD0,
                0x6F, // end、ref.null extern
            ],
            locals: [1, 1, 0x64, 0x6F]
        );
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");
        var value = new object();

        // Act
        var taken = function.Invoke(context, [WasmValue.FromExternRef(null)]).Values;
        var fallthrough = function.Invoke(context, [WasmValue.FromExternRef(value)]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(taken[0].AsI32()).IsEqualTo(3);
            await Assert.That(taken[1].AsI64()).IsEqualTo(4L);
            await Assert.That(taken[2].AsExternRef()).IsNull();
            await Assert.That(fallthrough[0].AsI32()).IsEqualTo(7);
            await Assert.That(fallthrough[1].AsI64()).IsEqualTo(8L);
            await Assert.That(fallthrough[2].AsExternRef()).IsSameReferenceAs(value);
        }
    }

    [Test]
    [Arguments("646F")] // (ref extern)
    [Arguments("6F")] // externref
    public async Task 参照を末尾に受けるラベルへbr_on_non_nullで分岐する_非nullなら参照を末尾に渡しnullなら取り除く(
        string labelReference
    )
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [2, 0x60, 1, 0x6F, 3, 0x7F, 0x7E, 0x6F, 0x60, 0, 3, 0x7F, 0x7E, .. Convert.FromHexString(labelReference)],
            [
                0x02,
                1, // block (result i32 i64 ref)
                0x41,
                9,
                0x41,
                3,
                0x42,
                4, // 9はラベルへ渡さない
                0x20,
                0,
                0xD6,
                0, // br_on_non_null 0 (local.get 0)
                0x42,
                1,
                0x7C,
                0xD0,
                0x6F,
                0x0F, // nullのときは残ったi64に1を加えて戻る
                0x0B,
            ]
        );
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");
        var value = new object();

        // Act
        var taken = function.Invoke(context, [WasmValue.FromExternRef(value)]).Values;
        var fallthrough = function.Invoke(context, [WasmValue.FromExternRef(null)]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(taken[0].AsI32()).IsEqualTo(3);
            await Assert.That(taken[1].AsI64()).IsEqualTo(4L);
            await Assert.That(taken[2].AsExternRef()).IsSameReferenceAs(value);
            await Assert.That(fallthrough[0].AsI32()).IsEqualTo(3);
            await Assert.That(fallthrough[1].AsI64()).IsEqualTo(5L);
            await Assert.That(fallthrough[2].AsExternRef()).IsNull();
        }
    }

    [Test]
    [Arguments("6470")] // (ref func)
    [Arguments("646F")] // (ref extern)
    public async Task 到達不能な位置で絞り込んだ参照を非nullの結果として使う_最初のunreachableでtrapになる(string resultType)
    {
        // Arrange
        var module = ReferenceControlModule.Create([1, 0x60, 0, 1, .. Convert.FromHexString(resultType)], [0x00, 0xD4]);
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act & Assert
        var exception = await Assert.That(() => function.Invoke(context, [])).ThrowsExactly<WasmTrapException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
    }

    [Test]
    public async Task 到達不能な位置で非nullのlocalに代入してから読む_最初のunreachableでtrapになる()
    {
        // Arrange
        var module = ReferenceControlModule.CreateWithNonNullableLocal("0021012001"); // unreachable、local.set 1、local.get 1
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromExternRef(new object())]))
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
    }
}
