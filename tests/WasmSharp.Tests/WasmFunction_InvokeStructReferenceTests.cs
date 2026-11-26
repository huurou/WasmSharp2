using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 空の構造体を繰り返し生成する_同じ実体への参照だけがWasmとホストの両方で等しい()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var allocate = ReferenceControlModule
            .Create([2, 0x5F, 0, 0x60, 0, 1, 0x6B], [0xFB, 1, 0], typeIndex: 1)
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var compare = ReferenceControlModule
            .Create([1, 0x60, 2, 0x6D, 0x6D, 1, 0x7F], [0x20, 0, 0x20, 1, 0xD3])
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var first = allocate.Invoke(context, []).Values[0];
        var second = allocate.Invoke(context, []).Values[0];
        var same = compare.Invoke(context, [first, first]).Values[0].AsI32();
        var different = compare.Invoke(context, [first, second]).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Kind).IsEqualTo(WasmValueKind.StructRef);
            await Assert.That(same).IsEqualTo(1);
            await Assert.That(different).IsEqualTo(0);
            await Assert.That(first != second).IsTrue();
        }
    }

    [Test]
    [Arguments((byte)0, (byte)1, true)] // 型番号の異なる同値型
    [Arguments((byte)1, (byte)1, true)] // 宣言された派生型
    [Arguments((byte)2, (byte)1, false)] // 無関係な構造体型
    [Arguments((byte)2, (byte)0x6B, true)] // struct
    [Arguments((byte)1, (byte)0x6D, true)] // eq
    [Arguments((byte)0, (byte)0x6E, true)] // any
    [Arguments((byte)2, (byte)0x6C, false)] // i31
    [Arguments((byte)0, (byte)0x6A, false)] // array
    public async Task 別モジュールの構造体の参照型を調べる_生成時の型に従って判定と分岐が一致する(
        byte sourceType,
        byte targetType,
        bool matches
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var value = AllocateStructReference(context, sourceType);
        var target = CreateStructCastFunctions(context, targetType);

        // Act
        var test = target.GetFunction("test").Invoke(context, [value]).Values[0].AsI32();
        var onCast = target.GetFunction("on_cast").Invoke(context, [value]).Values[0].AsI32();
        var onCastFail = target.GetFunction("on_cast_fail").Invoke(context, [value]).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(test).IsEqualTo(matches ? 1 : 0);
            await Assert.That(onCast).IsEqualTo(matches ? 1 : 0);
            await Assert.That(onCastFail).IsEqualTo(matches ? 0 : 1);
        }
    }

    [Test]
    [Arguments((byte)0, (byte)1)]
    [Arguments((byte)1, (byte)1)]
    [Arguments((byte)2, (byte)0x6B)]
    [Arguments((byte)1, (byte)0x6D)]
    [Arguments((byte)0, (byte)0x6E)]
    public async Task 別モジュールの構造体を代入可能な型にキャストする_同じ実体を返す(byte sourceType, byte targetType)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var value = AllocateStructReference(context, sourceType);
        var cast = CreateStructCastFunctions(context, targetType).GetFunction("cast");

        // Act
        var result = cast.Invoke(context, [value]).Values[0];

        // Assert
        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    [Arguments((byte)2, (byte)1)]
    [Arguments((byte)2, (byte)0x6C)]
    [Arguments((byte)0, (byte)0x6A)]
    public async Task 別モジュールの構造体を代入できない型にキャストする_キャスト失敗のtrapを返す(
        byte sourceType,
        byte targetType
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var value = AllocateStructReference(context, sourceType);
        var cast = CreateStructCastFunctions(context, targetType).GetFunction("cast");

        // Act & Assert
        var error = await Assert.That(() => cast.Invoke(context, [value])).ThrowsExactly<WasmTrapException>();
        await Assert.That(error!.Reason).IsEqualTo(WasmTrapReason.CastFailure);
    }

    [Test]
    [Arguments((byte)0, true)]
    [Arguments((byte)1, true)]
    [Arguments((byte)2, false)]
    public async Task 別モジュールの構造体を引数とホスト関数の戻り値に渡す_宣言型が受け入れる実体だけを返す(
        byte sourceType,
        bool matches
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var value = AllocateStructReference(context, sourceType);
        // 生成元の型0と同値の型を、このモジュールの型1として引数と戻り値に指定する。
        var echo = ReferenceControlModule
            .Create(Convert.FromHexString("0360000050005F017F0160016401016401"), [0x20, 0], typeIndex: 2)
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        var host = WasmFunction.CreateHost(WasmFunctionType.Create([], echo.Type.ResultTypes.AsSpan()), (_, _) =>
            new WasmResults([value])
        );

        if (!matches)
        {
            // Act & Assert
            using (Assert.Multiple())
            {
                await Assert.That(() => echo.Invoke(context, [value])).ThrowsExactly<ArgumentException>();
                await Assert.That(() => host.Invoke(context, [])).ThrowsExactly<InvalidOperationException>();
            }
            return;
        }

        // Act
        var argument = echo.Invoke(context, [value]).Values[0];
        var result = host.Invoke(context, []).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(argument).IsEqualTo(value);
            await Assert.That(result).IsEqualTo(value);
        }
    }

    [Test]
    public async Task 構造体を外部参照としてホストと往復する_元の実体のフィールドと参照の同一性を保つ()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var original = AllocateStructReference(context, 1);
        var known = new HashSet<WasmValue> { original };
        var (internalize, externalize) = CreateNonNullExternConversions(context);
        var writeAndRead = ReferenceControlModule
            .Create(
                [
                    2,
                    0x50,
                    0,
                    0x5F,
                    1,
                    0x7F,
                    1, // 生成元の派生型が宣言した上位型
                    0x60,
                    2,
                    0x64,
                    0,
                    0x64,
                    0,
                    1,
                    0x7F,
                ],
                [
                    0x20,
                    0,
                    0x41,
                    37,
                    0xFB,
                    5,
                    0,
                    0, // 最初の参照でフィールドを更新する。
                    0x20,
                    1,
                    0xFB,
                    2,
                    0,
                    0, // もう一方の参照で同じフィールドを読む。
                ],
                typeIndex: 1
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");

        // Act
        var token = externalize.Invoke(context, [original]).Values[0].AsExternRef();
        var external = WasmValue.FromExternRef(token);
        var restored = internalize.Invoke(context, [external]).Values[0];
        var updated = writeAndRead.Invoke(context, [restored, original]).Values[0].AsI32();
        var externalAgain = externalize.Invoke(context, [restored]).Values[0].AsExternRef();
        var externalOriginal = externalize.Invoke(context, [original]).Values[0].AsExternRef();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated).IsEqualTo(37);
            await Assert.That(restored).IsEqualTo(original);
            await Assert.That(known.Contains(restored)).IsTrue();
            await Assert.That(restored.AsStructRef()).IsSameReferenceAs(token);
            await Assert.That(externalAgain).IsSameReferenceAs(token);
            await Assert.That(externalOriginal).IsSameReferenceAs(token);
            // Act & Assert
            await Assert.That(external.AsStructRef).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 外部化した構造体の参照先をホスト参照として渡す_同じホストオブジェクトを取り出せる()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var (internalize, externalize) = CreateNonNullExternConversions(context);
        var structure = AllocateStructReference(context, 2);
        var token = externalize.Invoke(context, [structure]).Values[0].AsExternRef();
        var host = WasmValue.FromHostRef(token);

        // Act
        var external = externalize.Invoke(context, [host]).Values[0];
        var result = internalize.Invoke(context, [external]).Values[0];

        // Assert
        await Assert.That(result.AsHostRef()).IsSameReferenceAs(token);
    }

    private static WasmValue AllocateStructReference(WasmExecutionContext context, byte typeIndex)
    {
        var source = ReferenceControlModule
            .Create(
                [
                    4,
                    0x50,
                    0,
                    0x5F,
                    1,
                    0x7F,
                    1, // 型0は可変i32フィールドを持ち、派生を許す。
                    0x4F,
                    1,
                    0,
                    0x5F,
                    2,
                    0x7F,
                    1,
                    0x7E,
                    0, // 型1は型0を継承してi64フィールドを追加する。
                    0x5F,
                    0, // 型2は独立した空の構造体
                    0x60,
                    0,
                    1,
                    0x6B,
                ],
                [0xFB, 1, typeIndex],
                typeIndex: 3
            )
            .Validate()
            .Instantiate(context, [])
            .GetFunction("f");
        return source.Invoke(context, []).Values[0];
    }

    private static WasmInstance CreateStructCastFunctions(WasmExecutionContext context, byte targetType)
    {
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    4,
                    0x60,
                    0,
                    0, // 生成元の型0と同値の型を、こちらでは型1に置く。
                    0x50,
                    0,
                    0x5F,
                    1,
                    0x7F,
                    1,
                    0x60,
                    1,
                    0x6E,
                    1,
                    0x7F, // anyref -> i32
                    0x60,
                    1,
                    0x6E,
                    1,
                    0x6E, // anyref -> anyref
                ]
            ),
            HostLinkingModuleBinary.Functions(2, 3, 2, 2),
            HostLinkingModuleBinary.Exports(("test", 0, 0), ("cast", 0, 1), ("on_cast", 0, 2), ("on_cast_fail", 0, 3)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0xFB, 0x14, targetType, 0x0B]),
                ([], [0x20, 0, 0xFB, 0x16, targetType, 0x0B]),
                ([], Branch(0x18)),
                ([], Branch(0x19))
            )
        );
        return WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);

        byte[] Branch(byte opcode)
        {
            // 分岐時はblockの後で1を返す。継続時はblock内で0を返す。
            return
            [
                0x02,
                0x6E,
                0x20,
                0,
                0xFB,
                opcode,
                1,
                0,
                0x6E,
                targetType,
                0x1A,
                0x41,
                0,
                0x0F,
                0x0B,
                0x1A,
                0x41,
                1,
                0x0B,
            ];
        }
    }
}
