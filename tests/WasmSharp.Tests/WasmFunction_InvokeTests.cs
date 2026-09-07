using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmFunction_InvokeTests
{
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
