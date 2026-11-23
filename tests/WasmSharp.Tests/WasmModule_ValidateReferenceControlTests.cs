using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("200021012001")] // local.set 1、local.get 1
    [Arguments("200022011A2001")] // local.tee 1、drop、local.get 1
    [Arguments("2000210102646F20010B")] // 外側で初期化し、block内で読む
    [Arguments("200021010240200021010B2001")] // 内側で再代入しても、外側の初期化は有効
    public async Task 非nullの引数でlocalを初期化して読み取る_同じ参照を返す(string instructions)
    {
        // Arrange
        var module = CreateReferenceLocalModule(instructions);
        var context = new WasmExecutionContext();
        var value = new object();

        // Act
        var function = module.Validate().Instantiate(context, []).GetFunction("f");
        var result = function.Invoke(context, [WasmValue.FromExternRef(value)]).Values[0];

        // Assert
        await Assert.That(result.AsExternRef()).IsSameReferenceAs(value);
    }

    [Test]
    [Arguments("024020002101200022011A0B2001")] // block内で複数回代入した後、外側で読む
    [Arguments("0340200021010B2001")] // loop内で代入した後、外側で読む
    [Arguments("41000440200021010520011A0B2000")] // thenで初期化し、elseで読む
    [Arguments("410004402000210105200021010B2001")] // 両方の腕で初期化し、ifの後で読む
    [Arguments("002001")] // unreachableの直後で読む
    public async Task 読み取り位置で非nullのlocalが初期化されていない_未初期化を報告する(string instructions)
    {
        // Arrange
        var module = CreateReferenceLocalModule(instructions);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("uninitialized local");
    }

    [Test]
    public async Task 具体的な関数参照をref_as_non_nullで絞り込む_関数の同一性を保ちnullでtrapになる()
    {
        // Arrange
        var module = CreateReferenceModule(
            [2, 0x60, 0, 0, 0x60, 1, 0x63, 0, 1, 0x64, 0], // () -> () と (ref null 0) -> (ref 0)
            [0x20, 0, 0xD4],
            typeIndex: 1
        );
        var target = WasmFunction.CreateHost(new WasmFunctionType([], []), (_, _) => new WasmResults([]));
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act
        var result = function.Invoke(context, [WasmValue.FromFuncRef(target)]).Values[0];

        // Assert
        await Assert.That(result.AsFuncRef()).IsSameReferenceAs(target);

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromFuncRef(null)]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("null reference");
            await Assert.That(exception.Reason).IsEqualTo(WasmTrapReason.NullReference);
            await Assert.That(exception.Location?.Stage).IsEqualTo(WasmProcessingStage.Invoke);
        }
    }

    [Test]
    public async Task 整数2個をbr_on_nullで渡す_nullなら分岐し非nullなら絞り込んだ参照を返す()
    {
        // Arrange
        var module = CreateReferenceModule(
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
        var module = CreateReferenceModule(
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
    [Arguments("00D4451A")] // unreachable、ref.as_non_null、i32.eqz、drop
    [Arguments("00D500451A")] // unreachable、br_on_null 0、i32.eqz、drop
    public async Task 到達不能な位置で絞り込んだ参照を数値に使う_型不一致を報告する(string instructions)
    {
        // Arrange
        var module = CreateReferenceModule([1, 0x60, 0, 0], Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments("0170", "D0732000D5001A2101D070")] // br_on_nullの後で参照をdropし、残ったfuncrefをlocalへ渡す
    [Arguments("027070", "D0732000D6002101D070D070")] // br_on_non_nullの後で残ったfuncrefをlocalへ渡す
    public async Task 分岐しない経路でラベルの型に広がった値を元の狭い型に戻す_型不一致を報告する(
        string results,
        string instructions
    )
    {
        // Arrange
        var module = CreateReferenceModule(
            [1, 0x60, 1, 0x70, .. Convert.FromHexString(results)],
            Convert.FromHexString(instructions),
            locals: [1, 1, 0x73] // (local (ref null nofunc))
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments("4100D41A", "type mismatch")] // i32をref.as_non_nullに渡す
    [Arguments("4100D5001A", "type mismatch")] // i32をbr_on_nullに渡す
    [Arguments("027FD06FD5001A41000B1A", "type mismatch")] // br_on_nullに分岐値のi32が足りない
    [Arguments("D06FD600", "type mismatch")] // 空のラベル
    [Arguments("027FD06FD60041000B1A", "type mismatch")] // ラベルの末尾がi32
    [Arguments("026470D06FD600000B1A", "type mismatch")] // externrefを関数参照のラベルに渡す
    [Arguments("D06FD5011A", "unknown label 1")]
    [Arguments("D06FD601", "unknown label 1")]
    public async Task 参照を検査する命令の入力かラベルが不正である_検証時に原因を報告する(
        string instructions,
        string diagnostic
    )
    {
        // Arrange
        var module = CreateReferenceModule([1, 0x60, 0, 0], Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments("6470")] // (ref func)
    [Arguments("646F")] // (ref extern)
    public async Task 到達不能な位置で絞り込んだ参照を非nullの結果として使う_検証に成功し最初のunreachableでtrapになる(
        string resultType
    )
    {
        // Arrange
        var module = CreateReferenceModule([1, 0x60, 0, 1, .. Convert.FromHexString(resultType)], [0x00, 0xD4]);
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act & Assert
        var exception = await Assert.That(() => function.Invoke(context, [])).ThrowsExactly<WasmTrapException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
    }

    [Test]
    public async Task 到達不能な位置で非nullのlocalに代入してから読む_検証に成功し最初のunreachableでtrapになる()
    {
        // Arrange
        var module = CreateReferenceLocalModule("0021012001"); // unreachable、local.set 1、local.get 1
        var context = new WasmExecutionContext();
        var function = module.Validate().Instantiate(context, []).GetFunction("f");

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke(context, [WasmValue.FromExternRef(new object())]))
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.Unreachable);
    }

    private static WasmModule CreateReferenceLocalModule(string instructions)
    {
        // (param (ref extern)) (result (ref extern)) (local (ref extern))
        return CreateReferenceModule(
            [1, 0x60, 1, 0x64, 0x6F, 1, 0x64, 0x6F],
            Convert.FromHexString(instructions),
            locals: [1, 1, 0x64, 0x6F]
        );
    }

    private static WasmModule CreateReferenceModule(
        byte[] types,
        byte[] instructions,
        uint typeIndex = 0,
        byte[]? locals = null
    )
    {
        byte[] body = [.. locals ?? [0], .. instructions, 0x0B];
        return WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(1, types),
                HostLinkingModuleBinary.Functions(typeIndex),
                HostLinkingModuleBinary.Exports(("f", 0, 0u)),
                HostLinkingModuleBinary.Section(10, [1, .. HostLinkingModuleBinary.Unsigned((uint)body.Length), .. body])
            ),
            WasmSpecVersion.Core3
        );
    }
}
