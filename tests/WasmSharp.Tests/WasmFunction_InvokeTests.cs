using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("")]
    [Arguments("00060161FF0B0D00")]
    public async Task 非最短LEBと個数0のlocals宣言とcustomがある_両入力で定数の結果を変えない(
        string custom
    )
    {
        // Arrange
        // section長・型index・i32即値は合法な非最短LEB。customの名前以降は任意バイト。
        var bytes = Convert.FromHexString(
            "0061736D01000000"
                + custom
                + "018500016000017F"
                + custom
                + "0303018000"
                + custom
                + "0707010372756E0000"
                + custom
                + "0A09010701007F41AA000B"
                + custom
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        foreach (var module in modules)
        {
            module.Validate();
            var instance = module.Instantiate([]);
            var result = instance.GetFunction("run").Invoke([]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(result.Values.Length).IsEqualTo(1);
                await Assert.That(result.Values[0].Kind).IsEqualTo(WasmValueKind.I32);
                await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            }
        }
    }

    [Test]
    [Arguments(0x7F, WasmValueKind.I32, "4180808080780B", 0x80000000UL)]
    [Arguments(0x7F, WasmValueKind.I32, "41FFFFFFFF070B", 0x7FFFFFFFUL)]
    [Arguments(0x7E, WasmValueKind.I64, "428080808080808080807F0B", 0x8000000000000000UL)]
    [Arguments(0x7E, WasmValueKind.I64, "42FFFFFFFFFFFFFFFFFF000B", 0x7FFFFFFFFFFFFFFFUL)]
    [Arguments(0x7D, WasmValueKind.F32, "43000000000B", 0x00000000UL)]
    [Arguments(0x7D, WasmValueKind.F32, "43000000800B", 0x80000000UL)]
    [Arguments(0x7D, WasmValueKind.F32, "430000807F0B", 0x7F800000UL)]
    [Arguments(0x7D, WasmValueKind.F32, "43000080FF0B", 0xFF800000UL)]
    [Arguments(0x7D, WasmValueKind.F32, "434523C17F0B", 0x7FC12345UL)]
    [Arguments(0x7D, WasmValueKind.F32, "43230180FF0B", 0xFF800123UL)]
    [Arguments(0x7C, WasmValueKind.F64, "4400000000000000000B", 0x0000000000000000UL)]
    [Arguments(0x7C, WasmValueKind.F64, "4400000000000000800B", 0x8000000000000000UL)]
    [Arguments(0x7C, WasmValueKind.F64, "44000000000000F07F0B", 0x7FF0000000000000UL)]
    [Arguments(0x7C, WasmValueKind.F64, "44000000000000F0FF0B", 0xFFF0000000000000UL)]
    [Arguments(0x7C, WasmValueKind.F64, "44BC9A78563412F87F0B", 0x7FF8123456789ABCUL)]
    [Arguments(0x7C, WasmValueKind.F64, "44BC9A78563412F0FF0B", 0xFFF0123456789ABCUL)]
    public async Task 両入力のDecode後に元バッファを変更する_4段階で定数の型とビット列を保持する(
        int resultType,
        WasmValueKind expectedKind,
        string instructions,
        ulong expectedBits
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(
            (byte)resultType,
            Convert.FromHexString(instructions)
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var fromBytes = WasmModule.Decode(bytes);
        var fromStream = WasmModule.Decode(stream);
        Array.Clear(bytes);

        foreach (var module in new[] { fromBytes, fromStream })
        {
            module.Validate();
            var instance = module.Instantiate([]);
            var function = instance.GetFunction("run");
            var result = function.Invoke([]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(function.Type.Parameters).IsEmpty();
                await Assert.That(function.Type.Results.Single()).IsEqualTo(expectedKind);
                await Assert.That(result.Values.Length).IsEqualTo(1);
                var value = result.Values.Single();
                await Assert.That(value.Kind).IsEqualTo(expectedKind);
                var actualBits = expectedKind switch
                {
                    WasmValueKind.I32 => unchecked((uint)value.AsI32()),
                    WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
                    WasmValueKind.F32 => value.AsF32Bits(),
                    WasmValueKind.F64 => value.AsF64Bits(),
                    _ => throw new ArgumentOutOfRangeException(nameof(expectedKind)),
                };
                await Assert.That(actualBits).IsEqualTo(expectedBits);
            }
        }
    }

    [Test]
    public async Task 定数関数を反復し別の関数も呼ぶ_先に取得した結果と反復結果を保持する()
    {
        // Arrange
        var instance = WasmModule
            .Decode(
                ConstantModuleBinary.Create(
                    [(0x7F, [0x41, 0x7F, 0x0B]), (0x7F, [0x41, 0x2A, 0x0B])],
                    [("first", 0), ("second", 1)]
                )
            )
            .Validate()
            .Instantiate([], new(1));
        var function = instance.GetFunction("first");

        // Act
        var first = function.Invoke([]);
        var other = instance.GetFunction("second").Invoke([]);
        var repeated = function.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.Single().AsI32()).IsEqualTo(-1);
            await Assert.That(other.Values.Single().AsI32()).IsEqualTo(42);
            await Assert.That(repeated.Values.Single().AsI32()).IsEqualTo(-1);
        }
    }

    [Test]
    public async Task 引数の個数は合うが後続の型が異なる_実行前に引数不正として拒否する()
    {
        // Arrange
        // 引数を使う関数の実行は未対応なので、引数検査に必要な定義だけを内部構築する。
        var module = new WasmModule(
            [new([WasmValueKind.I32, WasmValueKind.F64], [WasmValueKind.I32])],
            [new(0, 30, [], [])],
            [],
            36
        );
        var function = new WasmInstance(module, WasmExecutionOptions.Default).Functions[0];

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke([WasmValue.FromI32(7), WasmValue.FromF32(1)]))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("arguments");
    }

    [Test]
    public async Task 引数なし関数に値を渡す_引数不正として拒否する()
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B))
            .Validate()
            .Instantiate([])
            .GetFunction("run");

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke([WasmValue.FromI32(7)]))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("arguments");
    }

    [Test]
    public async Task 定数関数を空の引数で呼び出す_宣言した型の結果を1個返す()
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x7F, 0x0B))
            .Validate()
            .Instantiate([], new(1))
            .GetFunction("run");

        // Act
        var result = function.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.Length).IsEqualTo(1);
            await Assert.That(result.Values[0].Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(-1);
        }
    }
}
